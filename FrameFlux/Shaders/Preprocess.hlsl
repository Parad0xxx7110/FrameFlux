// Params
Texture2D<float4> srcTex : register(t0);
RWTexture2D<float4> dstTex : register(u0);

cbuffer ResizeParams : register(b0)
{
    int srcWidth;
    int srcHeight;
    int dstWidth;
    int dstHeight;
};

[numthreads(16,16,1)]
void CSMain(uint3 DTid : SV_DispatchThreadID)
{
    uint x = DTid.x;
    uint y = DTid.y;

    if (x >= dstWidth || y >= dstHeight)
        return;

    // Source float coord
    float fx = (x + 0.5f) * srcWidth / dstWidth;
    float fy = (y + 0.5f) * srcHeight / dstHeight;

    int x0 = (int)fx;
    int y0 = (int)fy;
    int x1 = min(x0 + 1, srcWidth - 1);
    int y1 = min(y0 + 1, srcHeight - 1);

    float2 f = float2(fx - x0, fy - y0);

    float4 c00 = srcTex[int2(x0, y0)];
    float4 c10 = srcTex[int2(x1, y0)];
    float4 c01 = srcTex[int2(x0, y1)];
    float4 c11 = srcTex[int2(x1, y1)];

    float4 c0 = lerp(c00, c10, f.x);
    float4 c1 = lerp(c01, c11, f.x);
    float4 c = lerp(c0, c1, f.y);

    dstTex[int2(x, y)] = c; // RGBA float
}
