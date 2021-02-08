# 2D DXR PathTracer in Unity

Simple 2D PathTracer implemented in Unity and powered by DXR
Based off of https://github.com/SlightlyMad/SimpleDxrPathTracer
* Dead simple. Only few hundred lines of code. No Monte Carlo integration, no probability distribution functions, no importance sampling, a crude denoising implementation. It should be easy to follow (assuming you know what a path-tracer is)
* Implemented for default unity renderer
* Four simple material types (diffuse, metal, glass and emissive material)
* No analytical light sources, only emissive materials and a simple "emissive" background
* Not physically accurate
* Very simple, very slow

## Requirements
* Unity 2019.3.11f1 or newer
* DXR compatible graphics card (NVidia RTX series or some cars from NVidia GeForce 10 and 16 series)
* Windows 10 (v1809 or newer)

## No Denoising
![Alt text](DXR0.png?raw=true "Preview 1")
## Linear Accumulation
![Alt text](DXR1.png?raw=true "Preview 2")
## Exponential Moving Average
![Alt text](DXR2.png?raw=true "Preview 3")
## Edge-Avoiding À-TrousWavelet Transform
![Alt text](DXR3.png?raw=true "Preview 4")