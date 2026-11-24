// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace HarleAccel.Controls
{
    /// <summary>
    /// Vidro fosco: o Windows desfoca o que está atrás da janela (Windows 10 e 11).
    /// A cor e a transparência vêm do fundo do próprio app; aqui só liga/desliga o desfoque.
    /// </summary>
    public static class Glass
    {
        enum AccentState
        {
            Disabled = 0,
            EnableBlurBehind = 3, // desfoque simples: leve e sem travar ao arrastar a janela
        }

        [StructLayout(LayoutKind.Sequential)]
        struct AccentPolicy
        {
            public AccentState AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WindowCompositionAttributeData
        {
            public int Attribute; // 19 = WCA_ACCENT_POLICY
            public IntPtr Data;
            public int SizeOfData;
        }

        [DllImport("user32.dll")]
        static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

        /// <summary>Liga ou desliga o desfoque atrás da janela. Se o Windows não suportar, não faz nada.</summary>
        public static void SetBlur(Window w, bool on)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(w).Handle;
                if (hwnd == IntPtr.Zero) return;

                var accent = new AccentPolicy { AccentState = on ? AccentState.EnableBlurBehind : AccentState.Disabled };
                int size = Marshal.SizeOf(accent);
                IntPtr ptr = Marshal.AllocHGlobal(size);
                try
                {
                    Marshal.StructureToPtr(accent, ptr, false);
                    var data = new WindowCompositionAttributeData { Attribute = 19, Data = ptr, SizeOfData = size };
                    SetWindowCompositionAttribute(hwnd, ref data);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
            }
            catch
            {
                // Windows antigo ou API indisponível: fica só a transparência, sem desfoque
            }
        }
    }
}
