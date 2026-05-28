using AssetsTools.NET;
using AssetsTools.NET.Extra;
using AssetsTools.NET.Texture;
using SkiaSharp;
using System.Runtime.InteropServices;

namespace BetterCrewLink.HatTools;

class SpriteExporter(BundleLoader loader)
{
    const int CanvasW = 270, CanvasH = 428;
    const float HeadX = 139f, HeadY = 214f;
    // Skins anchor to the body origin (CosmeticsLayer world (0,0)), which sits 55px below HeadY.
    const float SkinY = HeadY + 55f;

    public void Export(AssetsFileInstance fileInst, AssetTypeValueField spritePtr, string outPath,
                       bool isSkin = false)
    {
        var spriteExt = loader.Manager.GetExtAsset(fileInst, spritePtr);
        if (spriteExt.baseField == null) return;
        var sf         = spriteExt.baseField;
        var spriteInst = spriteExt.file ?? fileInst;

        float anchorY = isSkin ? SkinY : HeadY;
        float pixelsToUnits = sf["m_PixelsToUnits"].AsFloat;
        float px = sf["m_Pivot"]["x"].AsFloat, py = sf["m_Pivot"]["y"].AsFloat;
        float rw = sf["m_Rect"]["width"].AsFloat, rh = sf["m_Rect"]["height"].AsFloat;
        var rd = sf["m_RD"];

        float trx, try_, trw, trh, trox, troy;
        AssetTypeValueField texPtrField;
        AssetsFileInstance  texSrcInst;

        var atlas = TryGetAtlasTextureData(spriteInst, sf);
        if (atlas.HasValue)
        {
            (trx, try_, trw, trh, trox, troy, texPtrField, texSrcInst) = atlas.Value;
        }
        else
        {
            var trect = rd["textureRect"];
            trx  = (float)Math.Floor(trect["x"].AsFloat);
            try_ = (float)Math.Floor(trect["y"].AsFloat);
            trw  = (float)Math.Ceiling(trect["width"].AsFloat);
            trh  = (float)Math.Ceiling(trect["height"].AsFloat);
            trox = rd["textureRectOffset"]["x"].AsFloat;
            troy = rd["textureRectOffset"]["y"].AsFloat;
            texPtrField = rd["texture"];
            texSrcInst  = spriteInst;
        }

        int srcX = (int)trx, srcY = (int)try_, srcW = (int)trw, srcH = (int)trh;
        int dstX = (int)Math.Round(HeadX - px * rw + trox);
        int dstY = (int)Math.Round(anchorY - (1f - py) * rh + rh - troy - trh);

        var texData = DecodeTexture(texSrcInst, texPtrField,
            out int texW, out int texH);
        if (texData == null) return;

        using var texBmp = FlipTextureVertical(texData, texW, texH);
        using var canvas = CreateCanvas(out var canvasBmp);

        var (meshIndices, meshVertices) = ReadSpriteMesh(spriteInst, rd);
        if (meshIndices.Length >= 3 && meshIndices.Length % 3 == 0 && meshVertices.Length >= 6)
            ClipToSpriteMesh(canvas, meshIndices, meshVertices, pixelsToUnits, anchorY);

        using var texImg = SKImage.FromBitmap(texBmp);
        canvas.DrawImage(texImg, dstX - srcX, dstY - (texH - srcY - srcH));

        using var img     = SKImage.FromBitmap(canvasBmp);
        using var encoded = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs      = File.Create(outPath);
        encoded.SaveTo(fs);
    }

    byte[]? DecodeTexture(AssetsFileInstance texSrcInst, AssetTypeValueField texPtrField,
        out int texW, out int texH)
    {
        texW = texH = 0;
        var texExt = loader.Manager.GetExtAsset(texSrcInst, texPtrField);
        if (texExt.baseField == null) return null;
        var texInst  = texExt.file ?? texSrcInst;
        var texField = loader.Manager.GetBaseField(texInst, texExt.info!);
        var texFile  = TextureFile.ReadTextureFile(texField);
        var encData  = texFile.FillPictureData(texInst);
        if (encData == null || encData.Length == 0) return null;
        var texData = texFile.DecodeTextureRaw(encData, useBgra: true);
        if (texData == null) return null;
        texW = texFile.m_Width;
        texH = texFile.m_Height;
        return texData;
    }

    // Texture is y-up (row 0 = bottom); flip to y-down for SkiaSharp.
    static SKBitmap FlipTextureVertical(byte[] texData, int texW, int texH)
    {
        var bmp = new SKBitmap(texW, texH, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var ptr = bmp.GetPixels();
        for (int row = 0; row < texH; row++)
            Marshal.Copy(texData, (texH - 1 - row) * texW * 4, ptr + (nint)(row * texW * 4), texW * 4);
        return bmp;
    }

    static SKCanvas CreateCanvas(out SKBitmap bitmap)
    {
        bitmap = new SKBitmap(CanvasW, CanvasH, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        bitmap.Erase(SKColors.Transparent);
        return new SKCanvas(bitmap);
    }

    // Clip canvas to the sprite's tight mesh so neighboring atlas sprites don't bleed through.
    static void ClipToSpriteMesh(SKCanvas canvas, ushort[] indices, float[] verts, float ptu, float anchorY)
    {
        using var path = new SKPath();
        for (int i = 0; i + 2 < indices.Length; i += 3)
        {
            int ai = indices[i] * 3, bi = indices[i + 1] * 3, ci = indices[i + 2] * 3;
            path.AddPoly([
                new SKPoint(HeadX + verts[ai]     * ptu, anchorY - verts[ai + 1] * ptu),
                new SKPoint(HeadX + verts[bi]     * ptu, anchorY - verts[bi + 1] * ptu),
                new SKPoint(HeadX + verts[ci]     * ptu, anchorY - verts[ci + 1] * ptu),
            ], close: true);
        }
        canvas.ClipPath(path, SKClipOperation.Intersect, antialias: true);
    }

    // Look up the correct textureRect from the SpriteAtlas for packed sprites.
    (float trx, float try_, float trw, float trh, float trox, float troy,
     AssetTypeValueField texPtr, AssetsFileInstance texInst)?
    TryGetAtlasTextureData(AssetsFileInstance spriteInst, AssetTypeValueField sf)
    {
        var atlasPtr = sf["m_SpriteAtlas"];
        if (atlasPtr.IsDummy || atlasPtr["m_PathID"].AsLong == 0) return null;

        var atlasExt = loader.Manager.GetExtAsset(spriteInst, atlasPtr);
        if (atlasExt.baseField == null) return null;
        var atlasInst = atlasExt.file ?? spriteInst;

        var rdk = sf["m_RenderDataKey"];
        if (rdk.IsDummy) return null;
        var rdkFirst = rdk["first"];
        uint d0 = rdkFirst[0].AsUInt, d1 = rdkFirst[1].AsUInt,
             d2 = rdkFirst[2].AsUInt, d3 = rdkFirst[3].AsUInt;
        long d4 = rdk["second"].AsLong;

        foreach (var pair in atlasExt.baseField["m_RenderDataMap"]["Array"].Children)
        {
            var k = pair["first"]["first"];
            if (k[0].AsUInt != d0 || k[1].AsUInt != d1 || k[2].AsUInt != d2 || k[3].AsUInt != d3) continue;
            if (pair["first"]["second"].AsLong != d4) continue;

            var v   = pair["second"];
            var tr  = v["textureRect"];
            var tro = v["textureRectOffset"];
            return (tr["x"].AsFloat, tr["y"].AsFloat, tr["width"].AsFloat, tr["height"].AsFloat,
                    tro["x"].AsFloat, tro["y"].AsFloat, v["texture"], atlasInst);
        }
        return null;
    }

    static (ushort[] indices, float[] vertices) ReadSpriteMesh(
        AssetsFileInstance spriteInst, AssetTypeValueField rd)
    {
        try
        {
            var indBytes = rd["m_IndexBuffer.Array"].AsByteArray;
            if (indBytes == null || indBytes.Length < 6) return ([], []);
            var indices = new ushort[indBytes.Length / 2];
            for (int i = 0; i < indBytes.Length; i += 2)
                indices[i / 2] = (ushort)(indBytes[i] | (indBytes[i + 1] << 8));

            var vd = rd["m_VertexData"];
            if (vd.IsDummy) return (indices, []);
            int vertCount = vd["m_VertexCount"].AsInt;
            if (vertCount == 0) return (indices, []);

            var channels = vd["m_Channels.Array"].Children;
            if (channels.Count == 0) return (indices, []);

            bool is2019Plus = int.TryParse(
                spriteInst.file.Metadata.UnityVersion.Split('.')[0], out int mv) && mv >= 2019;

            // Compute per-stream stride from channel layout.
            var streamStrides = new Dictionary<int, int>();
            foreach (var ch in channels)
            {
                int stream = ch["stream"].AsByte;
                int end    = ch["offset"].AsByte + (ch["dimension"].AsByte & 0xf) * VertexFormatSize(ch["format"].AsByte, is2019Plus);
                if (!streamStrides.TryGetValue(stream, out int cur) || end > cur)
                    streamStrides[stream] = end;
            }

            var streamStarts = new Dictionary<int, int>();
            int pos = 0;
            foreach (int s in streamStrides.Keys.OrderBy(k => k))
            {
                streamStarts[s] = pos;
                pos += streamStrides[s] * vertCount;
            }

            var vdBytes = ReadVertexDataBytes(spriteInst, rd, vd);
            if (vdBytes == null || vdBytes.Length == 0) return (indices, []);

            // Channel 0 is always vertex position.
            var posCh     = channels[0];
            int posStream = posCh["stream"].AsByte;
            int posOff    = posCh["offset"].AsByte;
            int posDim    = posCh["dimension"].AsByte & 0xf;
            int posFmtSz  = VertexFormatSize(posCh["format"].AsByte, is2019Plus);
            int posStride = streamStrides.GetValueOrDefault(posStream);
            int posStart  = streamStarts.GetValueOrDefault(posStream);

            var vertices = new float[vertCount * 3];
            for (int i = 0; i < vertCount; i++)
            {
                int b = posStart + i * posStride + posOff;
                vertices[i * 3 + 0] = BitConverter.ToSingle(vdBytes, b);
                vertices[i * 3 + 1] = BitConverter.ToSingle(vdBytes, b + posFmtSz);
                vertices[i * 3 + 2] = posDim > 2 ? BitConverter.ToSingle(vdBytes, b + posFmtSz * 2) : 0f;
            }
            return (indices, vertices);
        }
        catch
        {
            return ([], []);
        }
    }

    static byte[] ReadVertexDataBytes(
        AssetsFileInstance inst, AssetTypeValueField rd, AssetTypeValueField vd)
    {
        var streamData = rd["m_StreamData"];
        if (!streamData.IsDummy)
        {
            uint size   = streamData["size"].AsUInt;
            string path = streamData["path"].AsString;
            if (size > 0 && path.StartsWith("archive:/") && inst.parentBundle?.file is { } bundle)
            {
                uint offset = streamData["offset"].AsUInt;
                var  name   = Path.GetFileName(path["archive:/".Length..]);
                var  reader = bundle.DataReader;
                foreach (var dir in bundle.BlockAndDirInfo.DirectoryInfos)
                {
                    if (dir.Name != name) continue;
                    lock (reader)
                    {
                        reader.Position = dir.Offset + offset;
                        return reader.ReadBytes((int)size);
                    }
                }
            }
        }
        return vd["m_DataSize"].AsByteArray;
    }

    static int VertexFormatSize(int format, bool is2019Plus) =>
        is2019Plus
            ? format switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 2, 5 => 2, 6 => 1, 7 => 1, 8 => 2, 9 => 2, 10 => 4, 11 => 4, _ => 1 }
            : format switch { 0 => 4, 1 => 2, 2 => 1, 3 => 1, 4 => 1, 5 => 2, 6 => 2, 7 => 1, 8 => 1, 9 => 2, 10 => 2, 11 => 4, 12 => 4, _ => 1 };
}
