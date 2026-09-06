    float NebulaHash(float2 p)
    {
        p = frac(p * float2(123.34, 456.21));
        p += dot(p, p + 45.32);
        return frac(p.x * p.y);
    }
    float NebulaNoise(float2 p)
    {
        float2 cell = floor(p);
        float2 f = frac(p);
        f = f * f * (3.0 - 2.0 * f);
        return lerp(lerp(NebulaHash(cell), NebulaHash(cell + float2(1,0)), f.x),
            lerp(NebulaHash(cell + float2(0,1)), NebulaHash(cell + 1), f.x), f.y);
    }
    half4 NebulaGas(float2 flow, half mask, half peak, half3 gas, half3 core, float phase, float time)
    {
        float2 p = float2(flow.x * 18.0 + phase, flow.y * 3.5);
        float curl = NebulaNoise(p * .65 + float2(time, phase)) - .5;
        float y = flow.y + curl * .18;
        float2 wisp = float2(p.x * 4.2 - time, y * 15.0 + curl * 3.0);
        float n = NebulaNoise(wisp) * .6 + NebulaNoise(wisp * 2.07 + 8.3) * .28 + NebulaNoise(wisp * 4.13) * .12;
        float edge = 1.0 - smoothstep(.52, .98, abs(flow.y));
        float body = exp2(-y * y * 5.0) * (.72 + n * .4);
        float ridgeY = y + .12 + .08 * sin(p.x * .65 + curl * 2.0);
        float ridge = exp2(-ridgeY * ridgeY * 95.0) * (.35 + n * .85);
        float fine = pow(saturate(n * 1.35), 4.0) * body;
        float light = saturate(ridge * .65 + fine * .35);
        half3 color = lerp(gas * 1.08, core * 1.3, light);
        // Notches become density shadows rather than hard cuts through stacked polygons.
        half density = saturate(peak * (body * 1.2 + ridge * .32 + fine * .22) * edge * lerp(.55, 1.0, mask));
        return half4(color, density);
    }
