# Atmosphere Scattering
## Volume Rendering
Volume rendering is the foundation of atmosphere rendering.
### Concepts
#### Light absorption
As light travels through the volume in the direction of our eye (which is how images of objects we are seeing are formed in our eye), some of it will be absorbed by the volume as it passes through it. This phenomenon is called __absorption__.
The amount of light that's being transmitted through that volume is governed by the __Beer-Lambert law__ (or __Beer's law__ for short).
The Beer-Lambert law equation looks like this:

$T = exp(-distance * \sigma_a) = e^{-distatnce*\sigma_a}$

$\sigma_a$ is the representation of absorption coefficient of the volume.
#### In-Scattering
Volumes can be lit by light. As we can see, the scattering in the following image.

![](atmosphere_scattering/in_scattering1.PNG)

But the radiance reaches our eye is not just one light beam showing above. We must integrate all the scattering points along the eye ray.

![](atmosphere_scattering/in_scattering2.PNG)

We need to integrate the light that's be redirected towards the eye due to in-scattering along the segment of the ray that passes through the volumetric object.
Because there is no analytic solution to integration, so we can use Riemann sum to estimate the integration. If we know the distance from t0 to t1 and divide the distance into several segments, we can calculate the integration.

![](atmosphere_scattering/in_scattering3.PNG)

We assume that function Li(x) is the the scattering radiance reaches the eye form the point x. The lesson do not give the function of $L_i(x)$. So I guess the function $L_i(x)$ could be this:

$L_i(x) = Color_{light}(x) \times e ^{-(x - t_0) \times \sigma_a}$

$Color_{light}(x)$ is the color from the light reaches the point.
The following image shows the estimation of integration.

![](atmosphere_scattering/in_scattering4.PNG)

#### The Phase Function
When in-scattering happen, how much radiance should be scattered to a specific direction? The answer is deal to the phase function. 
We look at the in-scattering contribution using the following equation:

$L_i(x, \omega)= \sigma_s\int_{S^2}p(x,\omega,\omega')L(x,\omega')d\omega'$

Where $p(x,\omega,\omega')$ is the phase function.
I consider the phase function as a probability density function. And the integration of the phase function over a sphere is one.

$\int_{S^2}f_p(x,\omega,\omega')d\omega' = 1$

#### Optical Depth
It is actually transmittance, the amount of attenuated light after it passes a distance.

## Light Scattering Fundamentals
So we consider the atmosphere scattering to be one kind of volume rendering. We can think of the atmosphere as a large sphere-volume containing the whole earth.
After gaining a sufficient understanding of volume rendering, we can focus on atmosphere rendering, and I will mention the mathematical model first to make a clear understanding.
### Mathematical model
Both paper [2] and paper [3] mention the mathematical model of the atmosphere scattering. They are almost the same, so I just list the equations and explain what they demonstrate.
First, we must know the scattering intensity. It means the amount of light scattered at a point by given angle θ.
The equation of scattering intensity is:

$\bf I_{S_{R,M}}(\lambda,\theta,P)=I_I(\lambda)\rho_{R,M}(h)F_{R,M}(\theta)\beta_{R,M}(\lambda)$

Where $\lambda$ is the wavelength of the light.
$\bf I_l(\lambda)$ is the intensity of the incoming light.
$R, M$ refers to Rayleigh and Mie respectively.
$\rho_{R,M}(h)$ represents the Rayleigh/Mie density function where h is the altitude.

I just use the image from paper [2] here:

![](atmosphere_scattering/concepts.png)

Rayleigh and Mie have different functions respectively.
Here is the phase function of Rayleigh:

$F_R=\frac{3}{4}(1+\cos^2(\theta))$

Here is the phase function of Mie:

$F_M(\theta, g)=\frac{3(1-g^2)}{2(2+g^2)}\frac{(1+\cos^2(\theta))}{(1+g^2-2g\cos(\theta))^{3/2}}$

And here is the Scattering coefficient definition.
Rayleigh/Mie particle polarisability constant:

$\alpha_{R,M}=\frac{2\pi^2(n_e^2-1)}{3N_{e_{R,M}}^2}$

The Rayleigh and Mie scattering coefficients $\beta_R(\lambda)$ and $\beta_M()$ are then defined as 
$\beta_R(\lambda) = 4\pi\frac{N_R}{\lambda^4}\alpha_R$
$\beta_M()=4\pi N_M \alpha _M$

There is a little confusion about the coefficient $N_{e_{R,M}}$and $N_{R,M}$. $N_{e_{R,M}}$ represent the Rayleigh/Mie particles’ molecular number density of the Earth’s atmosphere at sea level. While $N_{R,M}$ denotes molecular number density of Rayleigh/Mie particles in the desired atmosphere.
In this way, Rayleigh's coefficient can be a constant while Mie's coefficient must be defined customly.
>Note: As aerosol particles both scatter and absorb light, the extinction of the mie coefficient is equal to $\beta_M + \beta_{M_a}$ ($\beta_{M_a}$ is the absorption coefficient). And we use $\beta_{M_a} = \beta_M / 0.9$ suggested by Bruneton and Neyret[BN08].
### Density Function
Density Function is defined as follows:
$\rho_{R,M}(h)=exp\big (-\frac{h}{H_{R,M}} \big)$
It is easy to implement, we need to know HR and HM：
$H_R \approx  8000m$
$H_M \approx 1200m$
### Transmittance Function
Transmittance is also optical depth. The equation of calculating transmittance is:

$t_{R,M}(S,\lambda)=\beta_{R,M}^e(\lambda)\int_0^S\rho_{R,M}(s')ds'$

Where $S$ is the distance of the transmittance.

$\beta^{e}_{R} = \beta_R ,  
\beta^{e}_{M} = \beta_M + \beta^{a}_M$

### Single Scattering
There are infinite points can be scattered into the direction the eyes look at. So the scattering light reaches to the eye should be integrated by $\bf \it I_{S_{R,M}}$ , not only the scattering point to the eye but also the sunlight reaches to the scattering point.

$I^{(1)}_{S_{R,M}}(P_O, V, L, \lambda) = I_I(\lambda) F_{R,M}(\theta) \frac{\beta_{R,M}(\lambda)}{4\pi}
\cdot \int_{P_a}^{P_b} \rho_{R,M}(h) \exp(-t_{R,M}(PP_C, \lambda) - t_{R,M}(P_a P, \lambda)) ds$

Where $P_c$ $P_b$ $P_a$ and $P_o$ can be described in this image:

![](atmosphere_scattering/single_scattering.PNG)

The (k) in the upright corner of the function $I_S^{(k)}$ means the scattering order. It is something to do with the multiple scattering which will be mentioned immediately.

### Multiple Scattering
In fact, what we see in the sky is not just single scattering. The light beam will scatter many times in a variety of directions and then finally scatter into the view direction. This is called multiple scattering.
We assume that the light beam scatter into our eye at point P scatter k times and then into the same point P, then the scattering light amount can be defined as follows:

$G_{R,M}^{(k)}(P,V,L,\lambda)=\int_{4\pi}F_{R,M}(\theta)I_{S_{R,M}}^{(k)}(P,\omega,L,\lambda)d\omega$

Where $\theta$ is the angle between $V$ and $\omega$.
This is defined as the gathering function of order k expresses the light gathered in point P, where the light has undergone exactly k scattering events.

![](atmosphere_scattering/multiple_scattering.PNG)

Each order of the scattering is defined below:

$`I_{S_{R,M}}^{(k)}(P,\omega,L,\lambda)=\frac{\beta_{R,M}(\lambda)}{4\pi} \cdot \int_{P_a}^{P_b}G_{R,M}^{(k-1)}(P,V,L,\lambda)\beta_{R,M}(\lambda)exp(-t_{R,M}(P_aP,\lambda))ds`$ 

Add up all the scattering order, we can get the final scattering intensity as

$I_S=\Sigma_{i=1}^KI_S^{(k)} $

So we can see the computation of multiple scattering is very complicated. There is an integration to calculate $I_S$ in $G$, and $G$ is also an integration in the last order. Then the whole computation includes integration in a recursion.
To make a deeper understanding, we should know that each point p along the view direction can scatter not only the sunlight (single scattering) but also all possible orders scattering.
This means that point p scatter light includes single scattering, 1 order scattering, 2 order scattering to infinite order scattering. But no matter how many orders of the scattering happen, the input light directions are the whole sphere surrounding the point. This explains why we should use the previous order of the scattering as the current order scattering input.

## Implementation
According to the theory, it is easy to implement if we just calculate the single scattering. Multiple scattering needs precomputation and I will mention it later.

### Realtime Single Scattering
We first define the input parameters we need. According to the equation of scattering, we will define view vector, light vector, light radiance and eye position as inputs.

$`I_{S_{R,M}}^{(1)}(P,\omega,L,\lambda)=I_I(\lambda)\rho_{R,M}(h)F_{R,M}(\theta)\frac{\beta_{R,M}(\lambda)}{4\pi} \cdot \int_{P_a}^{P_b} \rho_{R,M}(h) \exp(-t_{R,M}(PP_c, \lambda) - t_{R,M}(P_a P, \lambda)) ds`$ 

For each point P along the ray from $P_a$ to $P_b$ , $PP_c$ is the ray from the point to the sun and PPa is the ray from the sample point to the camera. [5]
The second step is we should define constants which are mentioned by the theory.

#### Scattering Coefficient
According to the definition of scattering coefficient, we need to define Rayleigh/Mie particles’ molecular number density at sea level and also the refractive index of air. As paper [4] says, the Rayleigh scattering coefficient is defined as follows:

$\beta_{R_{RGB}} = (6.55e−6, 1.73e−5, 2.30e−5)$

And the Mie scattering coefficient is independent of the wavelengths, so it is defined as follows:

$\beta_M()=4\pi N_M \alpha _M$

$\beta_{M_{RGB}} = (2e−6, 2e−6, 2e−6)$ 

where $N_m = 1$. 
Because we are not sure  the particle size of Mie scattering, we need to __apply a scale__ to this coefficient.

### Precompute Scattering
#### Analyze
Because the scattering calculation is based on integration. So we need to integrate all the possible paths of the eye rays. From the scattering equation, we could see there are 9 parameters we should consider. The camera position, the light direction, and the view direction. 
In order to reduce the number of parameters, we should make the follow assumptions [4]:

![](atmosphere_scattering/precompute_scattering.png)

we can also quote the assumptions from paper [2]:

![](atmosphere_scattering/precompute_scattering1.png)

All these assumptions can inspire us to eliminate the 9 dimensional table into a 4 dimensional table.
If we consider the vector formed by the planet circle point to the camera point as the Zenith. Then we can know the sunlight vector by these parameters, altitude $h$, view to zenith angle $\theta_v$, sun to zenith angle θs and sun to view azimuth angle $\theta_{\omega}$. Only these 4 parameters can determine the whole directions.
Look at this image from paper [4]:

![](atmosphere_scattering/precompute_scattering2.png)

This is the top view of the directions:

![](atmosphere_scattering/precompute_scattering_topview.png)

We can see that, no matter what the $\theta_{\omega}$ is, the integration of the transmittance will be the same if $\theta_{\omega} = 90^{\circ} - \theta_s$, and then set the $\theta_{\omega} = 0$. Because the distance from Pc to P, P to Pa will be the same. This deals with the assumption that the planet is perfectly a sphere.
According to the single scattering equation, there are two integrals should be calculated. One is the vector along the view direction, the other is the transmittance function which is inside the first integral.

#### Compute Transmittance Function
We can use the camera height and the θs to calculate all the possible integration of the Transmittance function.

$`t_{R,M}(S, \lambda)=\beta ^e_{R,M}(\lambda) \int_{0}^{S} \rho_{R,M}(s')ds' `$

The paper [4] introduces precompute the transmittance function by 2 parameters which are height and θs to the all possible directions from the camera to the top of the atmosphere transmittance.
But I decided not to use precompute, if I use steps equals 64 in precompute the skybox lut pass to calculate the transmittance, it will not be very slow.

```c++
//transmittance calculation
float2 Transmittance(float3 rayStart, float3 rayEnd, float3 earthCenter)
{
    float distance = length(rayEnd - rayStart);
    float sampleCount = 64.0;
    float3 step = (rayEnd - rayStart) / sampleCount;
    float stepSize = length(step);
    float3 rayDir = normalize(rayEnd - rayStart);
    float2 densityIntegral = 0;
    for (float s = 0.5; s < sampleCount; s++)
    {
        float3 samplePoint = rayStart + step * s;
        float height = max(length(samplePoint - earthCenter) - _EarthRadius, 0);
        densityIntegral += GetAtmosphereDensity(height) * stepSize;
    }

    return densityIntegral;
}
```

#### Skybox LUT Precomputation
First, we should make sense of __what is being precomputed__.
Look at the single scattering equation again:

$`I_{S_{R,M}}^{(1)}(P,\omega,L,\lambda)=I_I(\lambda)\rho_{R,M}(h)F_{R,M}(\theta)\frac{\beta_{R,M}(\lambda)}{4\pi} \cdot \int_{P_a}^{P_b} \rho_{R,M}(h) \exp(-t_{R,M}(PP_c, \lambda) - t_{R,M}(P_a P, \lambda)) ds`$ 

According to the equation, the heaviest part of the equation is the integral form point a to point b.
The previous analysis has told us that if we knew the height, view to zenith angle $\theta_v$, sun to zenith angle $\theta_s$, we could calculate the integral by numerical method.
As we have analyzed before, we should use a 3d texture to store the single scattering result. And the texture parameters are camera height, $\cos\theta_v$, $\cos\theta_s$ respectively. And paper [4] illustrates that these three parameters should be parametrized to [0,1].
And we already know the range of the three parameters here:

![](atmosphere_scattering/skylut_precompute_formula1.png)

The most straightforward implementation of such a function would be to use a linear parametrization:

![](atmosphere_scattering/skylut_precompute_formula2.png)

And Paper [4] states that it is sufficient if the texture resolution is large enough. Otherwise we should improve the parametrization with a smaller texture. The details of parametrization are introduced in the chapter 4 of  paper [4] and I am not going to show it here.
And here is the parametrization result of the coordinates:

![](atmosphere_scattering/skylut_precompute_formula3.png)

![](atmosphere_scattering/skylut_precompute_formula4.png)

![](atmosphere_scattering/skylut_precompute_formula5.png)

But when we precompute the scattering, all we have is the coordinates, so we should apply a __reverse operation__ to calculate height, cv and cs:

![](atmosphere_scattering/skylut_precompute_formula6.png)

After parametrizing the texture coordinates, we can calculate the integration of single scattering.
And the scattering integration and trasmittance calculation codes are listed below:
```c++
//transmittance calculation
float2 Transmittance(float3 rayStart, float3 rayEnd, float3 earthCenter)
{
    float distance = length(rayEnd - rayStart);
    float sampleCount = 64.0;
    float3 step = (rayEnd - rayStart) / sampleCount;
    float stepSize = length(step);
    float3 rayDir = normalize(rayEnd - rayStart);
    float2 densityIntegral = 0;
    for (float s = 0.5; s < sampleCount; s++)
    {
        float3 samplePoint = rayStart + step * s;
        float height = max(length(samplePoint - earthCenter) - _EarthRadius, 0);
        densityIntegral += GetAtmosphereDensity(height) * stepSize;
    }

    return densityIntegral;
}

float4 IntegrateInScattering(float3 rayStart, float3 rayEnd, float3 sunLight, float3 earthCenter)
{
    float sampleCount = 64.0;
    float3 step = (rayEnd - rayStart) / sampleCount;
    float stepSize = length(step);
    float3 scatteringR = 0;
    float3 scatteringM = 0;
    float3 tRayleigh = 0;
    float3 tMie = 0;
    float3 preScatteringR = 0;
    float3 preScatteringM = 0;
    for (float i = 0.5; i < sampleCount; i++)
    {
        float3 samplePoint = rayStart + step * i;
        float height = abs(length(samplePoint - earthCenter) - _EarthRadius);
        float2 localDensity = exp(-height.xx / _HeightScales.xy);
        float3 tPAR = 0;
        float3 tPAM = 0;
        float2 densityPA = Transmittance(rayStart, samplePoint, earthCenter);
        float2 densityCP = 0;

        
        float2 intersection = RaySphereIntersection(samplePoint, -sunLight, earthCenter, _EarthRadius);
        
        if (intersection.x > 0)
        {
            // intersection with planet, write high density
            densityCP = 1e+20;
        }
        else
        {
            intersection = RaySphereIntersection(samplePoint, -sunLight, earthCenter, _EarthRadius + _AtmosphereHeight);
            //because we are in the atmosphere, intersection.x is always negative, so we use intersection.y as the raylength.
            float3 pc = samplePoint - intersection.y * sunLight;
```
#### Rendering skybox
I use a fullscreen method instead of a sphere mesh to draw the skybox. As paper [4] mentions that we should parameteralize the height, cv(view zenith) and cs(sun zenith) to LUT coordinates. Then we sample the LUT and calculate the scattering radiance by scattering equation.
If we want to simulate sun rendering, we can use the original Mie phase function.
```c++
//use traditional mie phase function to simulate sun rendering
//As g is close to 1, the phase function will be larger when cosTheta is close to 1.
float3 SunSimulation(float cosTheta)
{
    float g = 0.99;
    float g2 = g * g;

    float sun = pow(1.0 - g, 2.0) / (4.0 * PI * pow(1.0 + g2 - 2.0 * g * cosTheta, 1.5));
    return sun * 0.003;
}
```

### Multiple Scattering
If we understand the process of multiple scattering clearly, it will be easy to implement. Think about the single scattering LUT table, what does it miss? Yes, it misses the 1 to k order multiple scattering results. So each integration along the view direction should plus the current order gather light function result. Then use the output LUT as the input of the next order LUT precomputation.
This is the main process of computing multiple scattering:

![](atmosphere_scattering/mutiple_scattering_process.png)

In compute shader, we just need a pre-order(k-1) LUT table to calculate the gather function, and output the cur-order(k) LUT table as the next input table.

```c++
Texture3D skyboxLUT = SingleScatteringLut();

preOrder = CreateLUTRenderTexture("PreOrderLUT");
Graphics.CopyTexture(skyboxLUT, preOrder);
curOrderOutput = CreateLUTRenderTexture("CurOrderLUT");

for (int i = 0; i < MultipleScatteringOrder; ++i)
{
    precomputeScattering.SetTexture(kPrecomputeMultipleLUT,
                   AtmosphereShaderParameters._PreOrderScatteringLUT, preOrder);
    precomputeScattering.SetTexture(kPrecomputeMultipleLUT,
        AtmosphereShaderParameters._OutScatteringLUT, curOrderOutput);

    precomputeScattering.Dispatch(kPrecomputeMultipleLUT, skyboxLUTSize.x, skyboxLUTSize.y, skyboxLUTSize.z);
    finalOutput = curOrderOutput;
    RenderTexture tmp = preOrder;
    preOrder = curOrderOutput;
    curOrderOutput = tmp;
}
```
## Reference
- https://www.scratchapixel.com/lessons/3d-basic-rendering/volume-rendering-for-developers/intro-volume-rendering.html [1]

- https://old.cescg.org/CESCG-2009/papers/PragueCUNI-Elek-Oskar09.pdf [2]

- http://nishitalab.org/user/nis/cdrom/sig93_nis.pdf [3]

- https://citeseerx.ist.psu.edu/viewdoc/download?doi=10.1.1.75.5595&rep=rep1&type=pdf [4]

- https://developer.nvidia.com/gpugems/gpugems2/part-ii-shading-lighting-and-shadows/chapter-16-accurate-atmospheric-scattering [5]

- https://publications.lib.chalmers.se/records/fulltext/203057/203057.pdf [6]