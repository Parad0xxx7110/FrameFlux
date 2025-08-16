using System;
using System.IO;
using Vortice.Direct3D11;
using Vortice.D3DCompiler;

namespace FrameFlux.Shaders
{
    internal class ShaderPreprocessing
    {
        private int srcWidth;
        private int srcHeight;
        private const int dstWidth = 640;
        private const int dstHeight = 480;

        private ID3D11ComputeShader computeShader;

        /// <summary>
        /// Constructeur : prend les dimensions de la texture source et le device D3D11.
        /// </summary>
        public ShaderPreprocessing(ID3D11Texture2D srcTexture, int srcWidth, int srcHeight, ID3D11Device device)
        {
            this.srcWidth = srcWidth;
            this.srcHeight = srcHeight;

            // Compile le shader et crée le compute shader
            CompileShader(device);
        }

        /// <summary>
        /// Compile le HLSL compute shader et crée le compute shader GPU
        /// </summary>
        private void CompileShader(ID3D11Device device)
        {
            string shaderPath = "path/to/shader.hlsl"; // mettre le chemin réel
            if (!File.Exists(shaderPath))
                throw new FileNotFoundException($"Shader file not found: {shaderPath}");

            string shaderCode = File.ReadAllText(shaderPath);
            if (string.IsNullOrWhiteSpace(shaderCode))
                throw new InvalidOperationException("Shader code is empty");

            // Compile compute shader
            ReadOnlyMemory<byte> bytecode = Compiler.Compile(
                shaderCode,
                entryPoint: "main",
                sourceName: shaderPath,
                profile: "cs_5_0",
                shaderFlags: ShaderFlags.OptimizationLevel3
            );

            if (bytecode.IsEmpty)
                throw new InvalidOperationException("Shader compilation failed");

            // Convert ReadOnlyMemory<byte> -> byte[] pour CreateComputeShader
            byte[] bytecodeArray = bytecode.ToArray();

            // Crée le compute shader
            computeShader = device.CreateComputeShader(bytecodeArray);
        }

        /// <summary>
        /// Retourne le compute shader compilé
        /// </summary>
        public ID3D11ComputeShader GetComputeShader() => computeShader;

        public int SrcWidth => srcWidth;
        public int SrcHeight => srcHeight;
        public int DstWidth => dstWidth;
        public int DstHeight => dstHeight;
    }
}
