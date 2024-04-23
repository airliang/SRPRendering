#ifndef PCSS_INCLUDED
#define PCSS_INCLUDED

#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"

//shadowmap SAT
Texture2D _ShadowMapSAT;
float2 _ShadowMapSATSize;

#define DISK_SAMPLE_COUNT 64
// Fibonacci Spiral Disk Sampling Pattern
// https://people.irisa.fr/Ricardo.Marques/articles/2013/SF_CGF.pdf
//
// Normalized direction vector portion of fibonacci spiral can be baked into a LUT, regardless of sampleCount.
// This allows us to treat the directions as a progressive sequence, using any sampleCount in range [0, n <= LUT_LENGTH]
// the radius portion of spiral construction is coupled to sample count, but is fairly cheap to compute at runtime per sample.
// Generated (in javascript) with:
// var res = "";
// for (var i = 0; i < 64; ++i)
// {
//     var a = Math.PI * (3.0 - Math.sqrt(5.0));
//     var b = a / (2.0 * Math.PI);
//     var c = i * b;
//     var theta = (c - Math.floor(c)) * 2.0 * Math.PI;
//     res += "float2 (" + Math.cos(theta) + ", " + Math.sin(theta) + "),\n";
// }

static const float2 fibonacciSpiralDirection[DISK_SAMPLE_COUNT] =
{
    float2 (1, 0),
    float2 (-0.7373688780783197, 0.6754902942615238),
    float2 (0.08742572471695988, -0.9961710408648278),
    float2 (0.6084388609788625, 0.793600751291696),
    float2 (-0.9847134853154288, -0.174181950379311),
    float2 (0.8437552948123969, -0.5367280526263233),
    float2 (-0.25960430490148884, 0.9657150743757782),
    float2 (-0.46090702471337114, -0.8874484292452536),
    float2 (0.9393212963241182, 0.3430386308741014),
    float2 (-0.924345556137805, 0.3815564084749356),
    float2 (0.423845995047909, -0.9057342725556143),
    float2 (0.29928386444487326, 0.9541641203078969),
    float2 (-0.8652112097532296, -0.501407581232427),
    float2 (0.9766757736281757, -0.21471942904125949),
    float2 (-0.5751294291397363, 0.8180624302199686),
    float2 (-0.12851068979899202, -0.9917081236973847),
    float2 (0.764648995456044, 0.6444469828838233),
    float2 (-0.9991460540072823, 0.04131782619737919),
    float2 (0.7088294143034162, -0.7053799411794157),
    float2 (-0.04619144594036213, 0.9989326054954552),
    float2 (-0.6407091449636957, -0.7677836880006569),
    float2 (0.9910694127331615, 0.1333469877603031),
    float2 (-0.8208583369658855, 0.5711318504807807),
    float2 (0.21948136924637865, -0.9756166914079191),
    float2 (0.4971808749652937, 0.8676469198750981),
    float2 (-0.952692777196691, -0.30393498034490235),
    float2 (0.9077911335843911, -0.4194225289437443),
    float2 (-0.38606108220444624, 0.9224732195609431),
    float2 (-0.338452279474802, -0.9409835569861519),
    float2 (0.8851894374032159, 0.4652307598491077),
    float2 (-0.9669700052147743, 0.25489019011123065),
    float2 (0.5408377383579945, -0.8411269468800827),
    float2 (0.16937617250387435, 0.9855514761735877),
    float2 (-0.7906231749427578, -0.6123030256690173),
    float2 (0.9965856744766464, -0.08256508601054027),
    float2 (-0.6790793464527829, 0.7340648753490806),
    float2 (0.0048782771634473775, -0.9999881011351668),
    float2 (0.6718851669348499, 0.7406553331023337),
    float2 (-0.9957327006438772, -0.09228428288961682),
    float2 (0.7965594417444921, -0.6045602168251754),
    float2 (-0.17898358311978044, 0.9838520605119474),
    float2 (-0.5326055939855515, -0.8463635632843003),
    float2 (0.9644371617105072, 0.26431224169867934),
    float2 (-0.8896863018294744, 0.4565723210368687),
    float2 (0.34761681873279826, -0.9376366819478048),
    float2 (0.3770426545691533, 0.9261958953890079),
    float2 (-0.9036558571074695, -0.4282593745796637),
    float2 (0.9556127564793071, -0.2946256262683552),
    float2 (-0.50562235513749, 0.8627549095688868),
    float2 (-0.2099523790012021, -0.9777116131824024),
    float2 (0.8152470554454873, 0.5791133210240138),
    float2 (-0.9923232342597708, 0.12367133357503751),
    float2 (0.6481694844288681, -0.7614961060013474),
    float2 (0.036443223183926, 0.9993357251114194),
    float2 (-0.7019136816142636, -0.7122620188966349),
    float2 (0.998695384655528, 0.05106396643179117),
    float2 (-0.7709001090366207, 0.6369560596205411),
    float2 (0.13818011236605823, -0.9904071165669719),
    float2 (0.5671206801804437, 0.8236347091470047),
    float2 (-0.9745343917253847, -0.22423808629319533),
    float2 (0.8700619819701214, -0.49294233692210304),
    float2 (-0.30857886328244405, 0.9511987621603146),
    float2 (-0.4149890815356195, -0.9098263912451776),
    float2 (0.9205789302157817, 0.3905565685566777)
};

real2 ComputeFibonacciSpiralDiskSample(const in int sampleIndex, const in real diskRadius, const in real sampleCountInverse, const in real sampleCountBias)
{
    real sampleRadius = diskRadius * sqrt((real)sampleIndex * sampleCountInverse + sampleCountBias);
    real2 sampleDirection = fibonacciSpiralDirection[sampleIndex];
    return sampleDirection * sampleRadius;
}

real PenumbraSizePunctual(real Reciever, real Blocker)
{
    return abs((Reciever - Blocker) / Blocker);
}

real PenumbraSizeDirectional(real Reciever, real Blocker, real rangeScale)
{
    return abs(Reciever - Blocker) * rangeScale;
}

bool BlockerSearch(inout real averageBlockerDepth, inout real numBlockers, real lightArea, real3 coord, real2 sampleJitter, Texture2D shadowMap, SamplerState pointSampler, int sampleCount)
{
    real blockerSum = 0.0;
    real sampleCountInverse = rcp((real)sampleCount);
    real sampleCountBias = 0.5 * sampleCountInverse;
    real ditherRotation = sampleJitter.x;

    for (int i = 0; i < sampleCount && i < DISK_SAMPLE_COUNT; ++i)
    {
        real2 offset = ComputeFibonacciSpiralDiskSample(i, lightArea, sampleCountInverse, sampleCountBias);
        offset = real2(offset.x *  sampleJitter.y + offset.y * sampleJitter.x,
                       offset.x * -sampleJitter.x + offset.y * sampleJitter.y);

        real shadowMapDepth = SAMPLE_TEXTURE2D_LOD(shadowMap, pointSampler, coord.xy + offset, 0.0).x;

        if (COMPARE_DEVICE_DEPTH_CLOSER(shadowMapDepth, coord.z))
        {
            blockerSum  += shadowMapDepth;
            numBlockers += 1.0;
        }
    }
    averageBlockerDepth = blockerSum / numBlockers;

    return numBlockers >= 1;
}

real HDPCSS(real3 coord, real filterRadius, real2 scale, real2 offset, real2 sampleJitter, Texture2D shadowMap, SamplerComparisonState compSampler, int sampleCount)
{
    real UMin = offset.x;
    real UMax = offset.x + scale.x;

    real VMin = offset.y;
    real VMax = offset.y + scale.y;

    real sum = 0.0;
    real sampleCountInverse = rcp((real)sampleCount);
    real sampleCountBias = 0.5 * sampleCountInverse;
    real ditherRotation = sampleJitter.x;

    for (int i = 0; i < sampleCount && i < DISK_SAMPLE_COUNT; ++i)
    {
        real2 offset = ComputeFibonacciSpiralDiskSample(i, filterRadius, sampleCountInverse, sampleCountBias);
        offset = real2(offset.x *  sampleJitter.y + offset.y * sampleJitter.x,
                       offset.x * -sampleJitter.x + offset.y * sampleJitter.y);

        real U = coord.x + offset.x;
        real V = coord.y + offset.y;

        //NOTE: We must clamp the sampling within the bounds of the shadow atlas.
        //        Overfiltering will leak results from other shadow lights.
        //TODO: Investigate moving this to blocker search.
        // coord.xy = clamp(coord.xy, float2(UMin, VMin), float2(UMax, VMax));

        if (U <= UMin || U >= UMax || V <= VMin || V >= VMax)
            sum += SAMPLE_TEXTURE2D_SHADOW(shadowMap, compSampler, real3(coord.xy, coord.z)).r;
        else
            sum += SAMPLE_TEXTURE2D_SHADOW(shadowMap, compSampler, real3(U, V, coord.z)).r;
    }

    return sum / sampleCount;
}

//---------------------------------------------

#ifndef SHADER_API_GLES3
CBUFFER_START(ShadowPCSSVariables)
#endif
float _Softness;
int _PCF_Samples;
float _SoftnessFalloff;
#ifndef SHADER_API_GLES3
CBUFFER_END
#endif

#if defined(_SHADOW_PCSS)
static const float2 PoissonDiskSamples[64] = {
    float2(0.0617981, 0.07294159),
    float2(0.6470215, 0.7474022),
    float2(-0.5987766, -0.7512833),
    float2(-0.693034, 0.6913887),
    float2(0.6987045, -0.6843052),
    float2(-0.9402866, 0.04474335),
    float2(0.8934509, 0.07369385),
    float2(0.1592735, -0.9686295),
    float2(-0.05664673, 0.995282),
    float2(-0.1203411, -0.1301079),
    float2(0.1741608, -0.1682285),
    float2(-0.09369049, 0.3196758),
    float2(0.185363, 0.3213367),
    float2(-0.1493771, -0.3147511),
    float2(0.4452095, 0.2580113),
    float2(-0.1080467, -0.5329178),
    float2(0.1604507, 0.5460774),
    float2(-0.4037193, -0.2611179),
    float2(0.5947998, -0.2146744),
    float2(0.3276062, 0.9244621),
    float2(-0.6518704, -0.2503952),
    float2(-0.3580975, 0.2806469),
    float2(0.8587891, 0.4838005),
    float2(-0.1596546, -0.8791054),
    float2(-0.3096867, 0.5588146),
    float2(-0.5128918, 0.1448544),
    float2(0.8581337, -0.424046),
    float2(0.1562584, -0.5610626),
    float2(-0.7647934, 0.2709858),
    float2(-0.3090832, 0.9020988),
    float2(0.3935608, 0.4609676),
    float2(0.3929337, -0.5010948),
    float2(-0.8682281, -0.1990303),
    float2(-0.01973724, 0.6478714),
    float2(-0.3897587, -0.4665619),
    float2(-0.7416366, -0.4377831),
    float2(-0.5523247, 0.4272514),
    float2(-0.5325066, 0.8410385),
    float2(0.3085465, -0.7842533),
    float2(0.8400612, -0.200119),
    float2(0.6632416, 0.3067062),
    float2(-0.4462856, -0.04265022),
    float2(0.06892014, 0.812484),
    float2(0.5149567, -0.7502338),
    float2(0.6464897, -0.4666451),
    float2(-0.159861, 0.1038342),
    float2(0.6455986, 0.04419327),
    float2(-0.7445076, 0.5035095),
    float2(0.9430245, 0.3139912),
    float2(0.0349884, -0.7968109),
    float2(-0.9517487, 0.2963554),
    float2(-0.7304786, -0.01006928),
    float2(-0.5862702, -0.5531025),
    float2(0.3029106, 0.09497032),
    float2(0.09025345, -0.3503742),
    float2(0.4356628, -0.0710125),
    float2(0.4112572, 0.7500054),
    float2(0.3401214, -0.3047142),
    float2(-0.2192158, -0.6911137),
    float2(-0.4676369, 0.6570358),
    float2(0.6295372, 0.5629555),
    float2(0.1253822, 0.9892166),
    float2(-0.1154335, 0.8248222),
    float2(-0.4230408, -0.7129914),
};

inline float2 Rotate(float2 pos, float2 rotationTrig)
{
    return float2(pos.x * rotationTrig.x - pos.y * rotationTrig.y, pos.y * rotationTrig.x + pos.x * rotationTrig.y);
}

//find the blocker information for a shading point, given a finding range defined as searchUV.
//return value is a float2, where x is the average blocker depth, and y is the number of blockers
float2 FindBlocker(float2 uv, float depth, float searchUV, float2 receiverPlaneDepthBias, float2 rotationTrig, 
    Texture2D shadowMap, SamplerState pointSampler)
{
    float avgBlockerDepth = 0.0;
    float numBlockers = 0.0;
    float blockerSum = 0.0;

    for (int i = 0; i < _PCF_Samples; i++)
    {
        float2 offset = PoissonDiskSamples[i] * searchUV;

        //#if defined(ROTATE_SAMPLES)
        offset = Rotate(offset, rotationTrig);
        //#endif

        float shadowMapDepth = SAMPLE_TEXTURE2D_LOD(shadowMap, pointSampler, uv + offset, 0);

        float biasedDepth = depth;

#if defined(UNITY_REVERSED_Z)
        if (shadowMapDepth > biasedDepth)
#else
        if (shadowMapDepth < biasedDepth)
#endif
        {
            blockerSum += shadowMapDepth;
            numBlockers += 1.0;
        }
    }

    avgBlockerDepth = blockerSum / numBlockers;

#if defined(UNITY_REVERSED_Z)
    avgBlockerDepth = 1.0 - avgBlockerDepth;
#endif

    return float2(avgBlockerDepth, numBlockers);
}

float2 FindBlockerBySAT(float2 uv, float depth, float searchUV,
    Texture2D shadowMapSAT, SamplerState pointSampler)
{
    float2 radius = searchUV;
    float r = SAMPLE_TEXTURE2D(shadowMapSAT, pointSampler, uv + radius);
    float topleft = SAMPLE_TEXTURE2D(shadowMapSAT, pointSampler, float2(uv.x - radius.x, uv.y + radius.y));
    float bottomRight = SAMPLE_TEXTURE2D(shadowMapSAT, pointSampler, float2(uv.x + radius.x, uv.y - radius.y));
    float bottomLeft = SAMPLE_TEXTURE2D(shadowMapSAT, pointSampler, uv - radius);
    float avgBlockerDepth = (r - bottomRight - topleft + bottomLeft) / (radius.x * radius.y);
#if defined(UNITY_REVERSED_Z)
    avgBlockerDepth = 1.0 - avgBlockerDepth;
#endif
    return float2(avgBlockerDepth, 1);
}

float PCSS_PCF_Filter(float2 uv, float depth, float filterRadiusUV, float2 receiverPlaneDepthBias, float penumbra, float2 rotationTrig,
    Texture2D shadowMap, SamplerComparisonState compSampler)
{
    float sum = 0.0f;
#if defined(UNITY_REVERSED_Z)
    receiverPlaneDepthBias *= -1.0;
#endif
    //int PCF_Samples = 32;
    //for (int i = 0; i < samples; i++)
    for (int i = 0; i < _PCF_Samples; i++)
    {
        float2 offset = PoissonDiskSamples[i] * filterRadiusUV;

        //#if defined(ROTATE_SAMPLES)
        offset = Rotate(offset, rotationTrig);
        //#endif

        float biasedDepth = depth;

#if defined(USE_PCF_BIAS)
        biasedDepth += dot(offset, receiverPlaneDepthBias) * PCF_GradientBias;
#endif

        float value = SAMPLE_TEXTURE2D_SHADOW(shadowMap, compSampler, float3(uv.xy + offset, biasedDepth));

        sum += value;
    }

    //sum /= samples;
    sum /= _PCF_Samples;

    return sum;
}

float CaculatePCFKernelFilterRadius(float depth_blocker, float depth_receiver, float lightSize)
{
    float Penumbra = (depth_receiver - depth_blocker) * lightSize / depth_blocker;
}

float SampleShadowSAT(SamplerState samp, float2 center, float2 radius)
{
    float r = SAMPLE_TEXTURE2D(_ShadowMapSAT, samp, center + radius);
    float topleft = SAMPLE_TEXTURE2D(_ShadowMapSAT, samp, float2(center.x - radius.x, center.y + radius.y));
    float bottomRight = SAMPLE_TEXTURE2D(_ShadowMapSAT, samp, float2(center.x + radius.x, center.y - radius.y));
    float bottomLeft = SAMPLE_TEXTURE2D(_ShadowMapSAT, samp, center - radius);
    return (r - bottomRight - topleft + bottomLeft) / (radius.x * radius.y);
}

float PCSS(float4 coords, float2 receiverPlaneDepthBias, float random, float cascadeScale,
    Texture2D shadowMap, SamplerComparisonState compSampler, SamplerState samp)
{
    float2 uv = coords.xy;
    float depth = coords.z;
    //we can see zAwareDepth as the depth of the receiver
    float depthReceiver = depth;

#if defined(UNITY_REVERSED_Z)
    depthReceiver = 1.0 - depth;
    receiverPlaneDepthBias *= -1.0;
#endif


    // STEP 1: blocker search
    //float searchSize = Softness * (depth - _LightShadowData.w) / depth;
    float searchSize = cascadeScale * _Softness * saturate(depthReceiver - .02) / depthReceiver;

    float rotationAngle = random * 3.1415926;
    float2 rotationTrig = float2(cos(rotationAngle), sin(rotationAngle));
    float2 blockerInfo = FindBlocker(uv, depth, searchSize, receiverPlaneDepthBias, rotationTrig, shadowMap, samp);
   
    if (blockerInfo.y < 1)
    {
        //There are no occluders so early out (this saves filtering)
        return 1.0;
    }
#if defined(_VSM_SAT_FILTER)
    float shadow = SampleShadowSAT(samp, uv, searchSize);
#else
    // STEP 2: penumbra size
    //float penumbra = zAwareDepth * zAwareDepth - blockerInfo.x * blockerInfo.x;
    float dBlocker = blockerInfo.x;
    float penumbra = (depthReceiver - dBlocker);// / depthReceiver;

    if (_SoftnessFalloff > 0.01)
        penumbra = 1.0 - pow(1.0 - penumbra, _SoftnessFalloff);

    float filterRadiusUV = penumbra * _Softness * cascadeScale;
    //filterRadiusUV *= filterRadiusUV;

    // STEP 3: filtering

    float shadow = PCSS_PCF_Filter(uv, depth, filterRadiusUV, receiverPlaneDepthBias, penumbra, rotationTrig, shadowMap, compSampler);
#endif
    return shadow;
}

#endif
#endif