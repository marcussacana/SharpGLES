using System;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using Orbis.Internals;

namespace SharpGLES
{
	public class EGLDisplay : IDisposable
	{
		public int Width { get; private set; }
		public int Height { get; private set; }

		private IntPtr _nativeDisplay = IntPtr.Zero;
		private IntPtr _display;
		private IntPtr _surface;
		private IntPtr _context;
		private IntPtr _handle;
		
		/// <summary>
		/// Initialize the EGL and Piglet if running in PS4
		/// </summary>
		/// <param name="handle">Used only In PC</param>
		/// <param name="Width">Used only In PS4</param>
		/// <param name="Height">Used only In PS4</param>
		public EGLDisplay(IntPtr handle, int Width, int Height, ulong SystemSharedMemory, ulong VideoSharedMemory, ulong FlexibleSharedMemory, ulong VideoPrivateMemory)
		{
			this.Width = Width;
			this.Height = Height;
			
#if ORBIS
			InitializePiglet(SystemSharedMemory, VideoSharedMemory, FlexibleSharedMemory, VideoPrivateMemory);
			InitializeShaderCompiler();
#else
			_handle = handle;
#endif
			
			InitializeWindow();
			InitializeEL();
		}

		public void Dispose()
		{
			EGL.DestroySurface(_display, _surface);

			EGL.DestroyContext(_display, _context);
			
			EGL.Terminate(_display);

#if !ORBIS
			EGLDC.ReleaseDC(_handle, _nativeDisplay);
#else
			Marshal.FreeHGlobal(_handle);
#endif
		}

		public void SwapBuffers()
		{
			EGL.SwapBuffers(_display, _surface);
		}
#if ORBIS
		private unsafe void InitializeShaderCompiler()
		{
			if (GLES20.HasShaderCompiler)
				return;

			var ShaderModule = Kernel.LoadStartModule("libSceShaccVSH.sprx");

			if ((ShaderModule & 0x80000000) != 0)
			{
				Kernel.Log("OpenGL Shader Compiler Unavailable");
				return;
			}
			
			
			Kernel.Log("OpenGL Shader Compiler Found - Applying Patches...");
			if (!Kernel.GetModuleBase(EGL.Path, out long BaseAddress, out long Size))
			{
				Kernel.Log("OpenGL Shader Compiler Unavailable - Failed to find the Piglet Base Address");
				return;
			}

			bool Jailbroken = Kernel.IsJailbroken();

			if (!Jailbroken)
				Kernel.Jailbreak(0);
			
			//THX FLATZ I'M YOUR FAN
			/* XXX: patches below are given for Piglet module from 4.74 Devkit PUP */
			byte* pTarget = (byte*)BaseAddress+0x5451F;
			byte[] SetEAXTo1 = new byte[] { 0x31, 0xC0, 0xFF, 0xC0, 0x90 };

			Kernel.MemProtect(pTarget, SetEAXTo1.Length, Kernel.PROT_EXEC | Kernel.PROT_READ | Kernel.PROT_WRITE);

			for (int i = 0; i < SetEAXTo1.Length; i++)
			{
				pTarget[i] = SetEAXTo1[i];
			}

			Kernel.MemProtect((byte*)BaseAddress + 0xB2DEC, 3, Kernel.PROT_EXEC | Kernel.PROT_READ | Kernel.PROT_WRITE);
			Kernel.MemProtect((byte*)BaseAddress + 0xB2E21, 1, Kernel.PROT_EXEC | Kernel.PROT_READ | Kernel.PROT_WRITE);
			Kernel.MemProtect((byte*)BaseAddress + 0xB2E24, 4, Kernel.PROT_EXEC | Kernel.PROT_READ | Kernel.PROT_WRITE);
			
			/* Tell that runtime compiler exists */
			*((byte*)BaseAddress + 0xB2DEC) = 0;
			*((byte*)BaseAddress + 0xB2DED) = 0;
			*((byte*)BaseAddress + 0xB2DEE) = 1;
			*((byte*)BaseAddress + 0xB2E21) = 1;
			
			/* Inform Piglet that we have shader compiler module loaded */
			*(int*)(BaseAddress + 0xB2E24) = ShaderModule;

			if (!Jailbroken)
				Kernel.Unjailbreak();

			GLES20.HasShaderCompiler = true;
			Kernel.Log("OpenGL Shader Compiler Enabled");
		}
		private const uint KB = 1024;
		private const uint MB = KB * 1024;
		private const uint GB = MB * 1024;
		private void InitializePiglet(ulong SystemMemory, ulong VideoSharedMemory, ulong FlexibleMemory, ulong VideoPrivateMemory)
		{
			var Module  = Kernel.LoadStartModule(EGL.Path);

			if ((Module & 0x80000000) != 0)
			{
				throw new DllNotFoundException($"LoadStartModule({EGL.Path}) result 0x{Module:X8}");
			}

			if (VideoPrivateMemory > 0)
				SetCompositorMemory(SystemMemory, VideoSharedMemory, VideoPrivateMemory);
			
			EGL.ScePglConfig Config = new EGL.ScePglConfig();
			
			Config.size = (uint)Marshal.SizeOf(typeof(EGL.ScePglConfig));
			Config.flags = EGL.ORBIS_PGL_FLAGS_USE_COMPOSITE_EXT | EGL.ORBIS_PGL_FLAGS_USE_FLEXIBLE_MEMORY | 0x60;
			Config.processOrder = 1;
			Config.systemSharedMemorySize = SystemMemory;
			Config.videoSharedMemorySize = VideoSharedMemory;
			Config.maxMappedFlexibleMemory = FlexibleMemory;
			Config.drawCommandBufferSize = 3 * MB;
			Config.lcueResourceBufferSize = 3 * MB;
			Config.dbgPosCmd_0x40 = (uint)Width;
			Config.dbgPosCmd_0x44 = (uint)Height;
			Config.dbgPosCmd_0x48 = 0;
			Config.dbgPosCmd_0x4C = 0;
			Config.unk_0x5C = 2;

			if (!EGL.scePigletSetConfigurationVSH(Config))
			{
				throw new Exception("Set Piglet configuration Failed");
			}
		}

		private void SetCompositorMemory(ulong SystemShared, ulong VideoShared, ulong VideoPrivate)
		{
			sceSysmoduleLoadModuleByNameInternal("libSceCompositeExt", 0, 0, 0, 0);
            sceApplicationInitialize("libSceCompositeExt");
			var Result = sceCompositorInitWithProcessOrder(SystemShared, VideoShared, VideoPrivate, 1);

			if (Result != 0 && Result != 0x80D40003)
			{
				if (VideoShared + VideoPrivate > EGL.ORBIS_PGL_MAX_VIDEO_SHARED_MEM)
					throw new OutOfMemoryException("The sum of the Video Private/Shared Memory shouldn't be bigger than 512MB");
				
				throw new Exception("Failed to Initialize the Compositor");
			}
		}

		[DllImport("libSceSysmodule.sprx")]
        private static extern uint sceSysmoduleLoadModuleByNameInternal(string Name, ulong UnkA, ulong UnkB, ulong UnkC, ulong UnkD);
        [DllImport("libSceSysCore.sprx")]
		private static extern void sceApplicationInitialize(string Name);

        [DllImport("libSceCompositeExt.sprx")]
		private static extern uint sceCompositorInitWithProcessOrder(ulong SystemSharedMemorySize, ulong VideoSharedMemorySize, ulong VideoPrivateMemorySize, ulong ProcessOrder);
#endif
        private void InitializeWindow()
		{
#if !ORBIS
			_nativeDisplay = EGLDC.GetDC(_handle);
#endif
			IntPtr requestedRenderer = _nativeDisplay;

			/*if (requestedRenderer == RENDERER_D3D11)
			{
				requestedRenderer = Hook.EGL_D3D11_ONLY_DISPLAY_ANGLE;
			}*/

			_display = EGL.GetDisplay(requestedRenderer);

			int minor;
			int major;

			if (!EGL.Initialize(_display, out major, out minor))
			{
				throw new EGLException("Initialize failed.");
			}

			if (!EGL.BindAPI(EGL.EGL_OPENGL_ES_API))
			{
				throw new EGLException("BindAPI failed.");
			}
#if ORBIS
			_handle = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(EGL.ScePglWindow)));
			var Window = new EGL.ScePglWindow()
			{
				uWidth =  (uint)Width,
				uHeight =  (uint)Height
			};
			Marshal.StructureToPtr(Window, _handle, false);
#endif
		}

		private void InitializeEL()
		{
			
#if ORBIS
			int[] configAttributes =
			{
				EGL.EGL_RED_SIZE, 8,
				EGL.EGL_GREEN_SIZE, 8,
				EGL.EGL_BLUE_SIZE, 8,
				EGL.EGL_ALPHA_SIZE, 8,
				EGL.EGL_DEPTH_SIZE, 0,
				EGL.EGL_STENCIL_SIZE, 0,
				EGL.EGL_SAMPLE_BUFFERS, 0,
				EGL.EGL_SAMPLES, 4,  // This is for 4x MSAA.
				EGL.EGL_RENDERABLE_TYPE, EGL.EGL_OPENGL_ES2_BIT,
				EGL.EGL_SURFACE_TYPE, EGL.EGL_WINDOW_BIT,
				EGL.EGL_NONE
			};
#else
			int[] configAttributes =
			{
				EGL.EGL_RED_SIZE, 8,
				EGL.EGL_GREEN_SIZE, 8,
				EGL.EGL_BLUE_SIZE, 8,
				EGL.EGL_ALPHA_SIZE, 8,
				EGL.EGL_DEPTH_SIZE, 24,
				EGL.EGL_STENCIL_SIZE, 8,
				EGL.EGL_SAMPLE_BUFFERS, EGL.EGL_DONT_CARE,
				EGL.EGL_SAMPLES, 4,  // This is for 4x MSAA.
				EGL.EGL_NONE
			};
#endif

			IntPtr configs;

			if (!EGL.ChooseConfig(_display, configAttributes, out configs, 1, out _))
			{
				throw new EGLException("ChooseConfig failed.");
			}
			
#if ORBIS
			int[] surfaceAttributes =
			{
				EGL.EGL_RENDER_BUFFER, EGL.EGL_BACK_BUFFER, 
				EGL.EGL_NONE, EGL.EGL_NONE
			};
#else
			int[] surfaceAttributes =
			{
				EGLX.EGL_POST_SUB_BUFFER_SUPPORTED_NV, EGL.EGL_TRUE, 
				EGL.EGL_NONE, EGL.EGL_NONE
			};
#endif

			_surface = EGL.CreateWindowSurface(_display, configs, _handle, surfaceAttributes);
			
			int[] contextAttibutes =
			{
				EGL.EGL_CONTEXT_CLIENT_VERSION, 2, 
				EGL.EGL_NONE
			};
			
			_context = EGL.CreateContext(_display, configs, IntPtr.Zero, contextAttibutes);

			if (_context == IntPtr.Zero)
				throw new Exception("Failed to Create the EGL Context");

			EGL.MakeCurrent(_display, _surface, _surface, _context);

			EGL.SwapInterval(_display, 0);
		}
	}
}
