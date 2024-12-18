# Realtime Baking Atmosphere Scattering to Spherical Harmonics
## 1.Introduction
Atmosphere scattering is widely used in realtime rendering, to present a physically based sky. In this way, the atmosphere will impact the whole environmental lighting when the time-of-day system is enabled. The amount of environmental light is different during different time in a day. An efficient way to calculate environmental lighting is to use spherical harmonics to reconstruct the lighting function of the sky. Precompute Atmosphere Scattering( Nishita T. 1996) is a mature method for real-time multiple-scattering atmosphere rendering. Spherical harmonics is a great contribution to efficient global environmental lighting.

Our motivation is to bake atmosphere scattering light radiance into spherical harmonic coefficients in real-time. The process is implemented in compute shaders. And we use an efficient way(Mark Harris, NVidia) to calculate the Monte Carlo Integration of projecting the spherical harmonic coefficient. Besides, we also introduce an efficient way to prefilter atmosphere scattering to specular map used in image-based lighting. Physical Based Shading objects take advantage of the spherical harmonic coefficients as diffuse reflection and prefilter atmosphere scattering specular map as specular reflection.

## 2.Related Work
__Precomputed Atmosphere Scattering.__ Eric Bruneton introduced an efficient and dynamic atmospheric scattering based on precomputing lookup tables, which store precomputed scattering information at various locations in the atmosphere. This method results in a high performance of rendering sky and aerial perspective. These tables allow for efficient retrieval of scattering properties during rendering, reducing the need for extensive real-time calculations. Our approach builds upon this concept by combining it with parallel prefix sum techniques.
__Irradiance Volumes.__ Gene Greger introduced the concept of irradiance volume which represents a volumetric approximation of irradiance function. Natalya Tatarchuk use irradiance volumes for games by introducing spherical harmonics to represent irradiance volume. It is efficient and has a low memory cost in this way. We use spherical harmonics for environmental lighting approximation, in which its light radiance is sampled from atmosphere scattering.
__Parallel Prefix Sum.__ We introduce a parallel prefix sum technique to streamline the computation of spherical harmonic coefficients based on the pre-computed 3D lookup texture. By breaking down the problem into smaller tasks and utilizing the parallel processing capabilities of modern GPUs, our method achieves a significant reduction in computation time. The parallel prefix sum algorithm efficiently aggregates the scattering contributions along different directions, enabling the rapid determination of spherical harmonic coefficients.
__Image-Based Lighting.__ The reflected radiance R(v) in the view direction v is computed by integrating all the incoming radiance over a hemisphere H. Here we consider L(l) as the atmosphere scattering incoming radiance.

$`R(v) = \int _H L(l)f(l,v)(n \cdot l) \rm dl `$

Where $$f(l,v)$$ is the BRDF function, n is the surface normal of the viewing point.
Specular reflections are more complicated and we summarize the results from the paper that introduces GGX [WMLT07]. The general Cook-Torrance microfacet model for specular reflection is 

$f_{spec}(l,v) = \frac{D(h)G(l,v,h)F(v,h)}{4(n \cdot l)(n \cdot v) }$

where $D(h)$ is the microfacet normal distribution, $G(l, v,h)$ is the geometric masking term, and $F(v,h)$ is the Fresnel term. GGX provides a physically plausible definition for $D(h)$ and $G(l, v,h)$. Brian Karis epic game split the integration into two parts.

$`R(v) = \int _HL(l)\frac{D(h)G(l,v,h)F(v,h)}{4(n \cdot l)(n \cdot v) }(n \cdot l) \rm dl \approx (\int_H L(l)D(h)\rm dl)( \int_H \frac{G(l,v,h)F(v,h)}{4(n \cdot v)}\rm dl)`$

Our approach is to use compute shader to bake the diffuse part into spherical harmonic coefficients. For the specular part, we prefilter atmosphere scattering to a cubemap, since we consider atmosphere scattering as a low-frequency, we set the size of the cubemap to 32 could be enough.

## 3. Baking Atmosphere Scattering to Spherical Harmonics
In this section, we will describe spherical harmonic lighting and make a simple introduction to spherical harmonics. 
### 3.1 Spherical Harmonics Lighting
Consider a Lambert diffuse rendering equation:

$`L(x) = \frac{\rho_x }{\pi} \int _S {L_i(x, \omega _i)}max(N_x, \omega_i) d\omega_i`$    (1)

Where $\rho_x$ is the albedo at the surface point x. $N_x$ is the normal at the surface point x.
While the ambient lighting requires only the same input direction as normal, we eliminate the cosine term in the rendering equation. More, $\frac{\rho_x}{\pi} $ is applied in realtime shading of the material. Since our goal is to apply environmental lighting, we only concern the rendering function as follows:

$`L(x) = \int _S {L_i(x, \omega _i)} d\omega_i `$    (2)

Then we see the definition of the spherical harmonics function. Traditionally, the SH function is represented as symbol y:

$`y_{l}^{m}(\theta, \phi) = y_i(\theta, \phi)    where i = l(l+1)+m`$

The parameters $l$ and $m$ are defined slightly differently from the Legendre polynomials $l$ is still a positive integer starting from 0, but m takes signed integer values from $–l$ to $l$. 
The next process is to project a spherical function to the spherical harmonics coefficients. The equation for calculating coefficients is simple:

$`c _i = \int _S f(x)y_i(x)   dx \space \space \space \space \space`$   (3)

where $S$ is the spherical domain, $x$ is a direction in the spherical.  

We must evaluate this integral using Monte Carlo integration, so recalling the Monte Carlo estimator we have: 

$`c_i \approx \frac{1}{N} \sum _{j = 1}^{N}\frac{f(x_j)y_i(x_j)}{p(x_j)}`$    (4)

Here we consider the probability function of  $p(x_j) = \frac{1}{4\pi}$, the uniform spherical distribution. Then equation (4) becomes more simple:

$`c_i \approx \frac{4\pi}{N} \sum _{j = 1}^{N}f(x_j)y_i(x_j)`$    (5)

To reconstruct the approximated function $f(x)$, we sum the scaled corresponding spherical harmonic function in the specific n-th order:

$`\widetilde{f}(x) = \sum _{i = 0}^{n^2}c _i y_i(x)`$    (6)

In this way, we can approximate the lighting function by projecting it into spherical harmonic coefficients.

### 3.2 Gathering Atmosphere Scattering Light
According to Eric Bruneton's paper, it is very efficient to calculate the atmosphere scattering in real-time by using precomputed lookup tables. Due to this fact, it makes projecting atmosphere scattering into spherical harmonics coefficients possible, since we will execute this process only when the direction of the directional light is changed. 
Considering the single scattering equation:

$` I_{s_{R,M}}^{(1)}(P_O, V, L, \lambda) = I _i(\lambda)F_{R,M}(\theta) \frac{\beta_{R,M}}{4 \pi} \cdot \int _{P_a}^{P_b} \rho_{R,M}(h)exp(-t_{R,M}(PP_C, \lambda)-t_{R,M}(P_aP,\lambda))ds `$    (7)

We precompute the integral part of the equation into the 3-dimensional texture from which we sample in real-time rendering.
If we take into account the multiple scattering, the real-time rendering is the same except the precomputation. Just see the k-order multiple scattering equation:

$`I_{s_{R,M}}^{(k)}(P_O, V, L, \lambda) = I _i(\lambda)F_{R,M}(\theta) \frac{\beta_{R,M}}{4 \pi} \cdot \int _{P_a}^{P_b} G^{(k-1)}(P, V, L, \lambda) \rho_{R,M}(h)exp(-t_{R,M}(PP_a, \lambda))ds `$    (8)

Where

$`G^{(k-1)}(P, V, L, \lambda)=\int _{4 \pi} F_{R,M}(\theta)I^{k}_{S_{R,M}}(P,\omega, L, \lambda)d\omega `$    (9)

In this case, the only difference is the result of the precompute 3-dimensional texture. It is not a problem whether we use single scattering or multiple scattering.
### 3.3 Parallel Prefix Sum
We could observe the calculation of the integral in equation (3) to obtain the spherical harmonics coefficients. The better option is using the Monte Carlo method since there is no analyzing solution. The Monte Carlo method requires a large number of samples to approximate the result. Therefore, how to make the sum efficiently of those samples becomes a challenge. Parallel Prefix Sum is introduced to enhance the speed of the sum of the elements in an array. The procedure needs two passes to add the items so that the final element is equal to the sum of the previous elements. We call them up-pass and down-pass. 
We are given an ordered set A of n elements and a binary associative operator $\oplus$.

$A = \{a_0, a_1, a_2, ..., a_{n-1} \}$    (10)

Then we will result in the following order set:

$`A = \{a_0, (a_0 \oplus a_1), ..., (a_0 \oplus a_1 \oplus ... a_{n-1})\}`$    (11)

As we only need the last element of the output in this case, we can skip the down-pass of the parallel prefix sum algorithm to obtain the maximum speed.
An example of the up-pass:

![](bake_atmosphere_sh/presum.png)

The value of the last element is the result of the integral which is calculated by the up-pass.

## 4. Prefilter Atmosphere Scattering Specular

## 5. Implementation Details
__Precomputed Atmosphere Scattering.__ We have implemented the precomputation algorithm on GPU, with compute shaders processing the numerical integration. We store the integral into a 32 x 64 x 32 3D RGBA format texture, where the RGB channels store rayleigh scattering and the alpha channel stores mie scattering. We also optimized the parameterization of the sample coordinates to obtain better precision. As a result of the optimization, the storage of the 3D texture just requires 1M memory. When fetching the skylight, we just need to sample the 3D texture and then calculate the scattering result by equation (7) or (8). (It depends on whether we are using single scattering or multiple scattering).

__Project scattering to Spherical Harmonic Coefficients.__ The projecting is done in the following process.
Initialize the samples of the directions that are used to fetch skylight. We take 16 different polar angles and 32 different azimuthal angles to form the sample directions. We only generate samples once during the program at runtime. So we have a total of 512 samples and we divide every 128 samples into a single group. 
We dispatch the compute shader to calculate the sum of the array in each group. The array is filled by the element $f(x_j)y_i(x_j)$. $f(x_j)$ can represent equation (8) or (9) depending on single or multiple scattering in your program. As my program is baking the 3-order spherical harmonic coefficients, the amount of the coefficients is 9. In order to obtain the maximum parallel computing speed by GPU, we dispatch the computer shader with 4 x 9 x 1 thread groups and each group owns 128 x 1 x 1 threads. The thread numbers are defined for the parallel prefix sum algorithm. The group id of y represents the index of the coefficients. 
After the sum of each group array, we perform another kernel to sum up the last element of each group to get the final result of each coefficient. 
The final pass is to get the result of the group sum from the last step. Then each order element times a factor of $4 \pi / 512$ according to equation (5). Finally, we get the result of the coefficients of each order.

__Rendering.__ After getting the SH coefficients, we can apply them to the global shading parameters. In the lighting stage of a fragment shader, we use them to perform the environmental lighting. When the sun's direction is modified, we can perform an accurate approximation of environmental lighting.

__Limitations.__ One of the limitations is we have to read back the coefficients from GPU to GPU. If we attempt to read the data immediately after baking, a GPU stall will occur. This will cause less efficiency of the program. As a matter of fact, we intend to get back the data asynchronously. However, this approach may delay the matching data for several frames. But it is not obvious because the sun direction always changes slowly or maintains stable.

## 6. Results and Performance Evaluation
In this section, we present a detailed performance comparison between the parallel prefix sum-based approach and the traditional loop sum method for computing spherical harmonic coefficients for atmosphere scattering lighting using the provided test data. The tests were conducted on an NVIDIA RTX3060 video card.
### 6.1. Test Setup:
We performed the performance comparison using a representative scene with complex atmospheric scattering effects. The scene contained a variety of scattering interactions and lighting conditions, providing a comprehensive evaluation of both methods. The test data included scattering properties and lighting information required for accurate coefficient calculation.

### 6.2. Parallel Prefix Sum vs. Loop Sum:
#### 6.2.1. Computation Time:
The primary metric for performance evaluation is the computation time required by each method to calculate the spherical harmonic coefficients. The parallel prefix sum-based approach demonstrated superior efficiency, completing the coefficient calculation in an impressive 0.02ms. In contrast, the loop sum method took approximately 0.19ms to perform the same calculation. This notable speedup of nearly 9 times showcases the advantage of leveraging parallel computing for optimizing the computation-intensive task.
#### 6.2.2. Utilization of GPU Resources:
The parallel prefix sum method efficiently utilizes the processing capabilities of the NVIDIA RTX3060 video card. By leveraging the parallelism inherent in modern GPUs, the approach effectively distributes the computation load across multiple threads, allowing for the simultaneous processing of scattering contributions. This results in a higher GPU utilization, contributing to the significant reduction in computation time.
### 6.3. Comparison Summary:
Metric|Parallel Prefix Sum|Loop Sum
---|---|--- 
Computation Time|0.02ms|0.19ms
GPU Utilization|High|Moderate
Performance Advantage|~9x speedup|-

### 6.4. Practical Implications
The performance gain offered by the parallel prefix sum-based approach is of significant practical importance. Real-time rendering scenarios often demand rapid computation of complex lighting effects, especially in interactive applications. The substantial reduction in computation time enables the rendering of more detailed scenes or the allocation of additional computational resources to other rendering tasks, ultimately enhancing the overall quality and interactivity of the virtual environment.

### 6.5. Conclusion
In this performance comparison, the parallel prefix sum-based approach has demonstrated a remarkable advantage over the traditional loop sum method in terms of computation time and GPU utilization. By harnessing the power of parallel computing on the NVIDIA RTX3060 video card, our approach achieves a substantial speedup while maintaining rendering quality. This performance gain is of significant importance for real-time rendering applications, contributing to the advancement of atmospheric rendering techniques and immersive virtual environments.

## Reference
[Manson J and Sloan P: Fast Filtering of Reflection Probes](https://research.activision.com/publications/archives/fast-filtering-of-reflection-probes)

[SLOAN P.: Stupid spherical harmonics (SH) tricks, 2008.](https://www.ppsloan.org/publications/StupidSH36.pdf)

[Karis B: Real Shading in Unreal Engine 4](https://cdn2.unrealengine.com/Resources/files/2013SiggraphPresentationsNotes-26915738.pdf)
