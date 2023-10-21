using System;

namespace SharpGLES
{
    public struct EGLSettings
    {
        /// <summary>
        /// Windows GDI Handle - PC Only
        /// </summary>
        public IntPtr Handle;

        /// <summary>
        /// Rendering Width Resolution
        /// </summary>
        public int Width;
        /// <summary>
        /// Rendering Height Resolution
        /// </summary>
        public int Height;

        public ulong SystemSharedMemory;
        public ulong VideoSharedMemory;
        public ulong FlexibleSharedMemory;
        public ulong VideoPrivateMemory;


        /// <summary>
        /// When set to false, the shader compiler loads only on jailbroken processes.
        /// When set to true, the process is jailbroken to load the shader compiler if available.
        /// The shader compiler depends on the libScePigletv2VSH.sprx and libSceShaccVSH.sprx modules from the FlatZ OpenGL implementation.
        /// </summary>
        public bool ForceShaderCompiler;

        public EGLSettings(IntPtr handle, int Width, int Height, ulong SystemSharedMemory, ulong VideoSharedMemory, ulong FlexibleSharedMemory, ulong VideoPrivateMemory)
        {
            this.Handle = handle;
            this.Width = Width;
            this.Height = Height;
            this.SystemSharedMemory = SystemSharedMemory;
            this.VideoSharedMemory = VideoSharedMemory;
            this.FlexibleSharedMemory = FlexibleSharedMemory;
            this.VideoPrivateMemory = VideoPrivateMemory;

            ForceShaderCompiler = false;
        }
    }
}
