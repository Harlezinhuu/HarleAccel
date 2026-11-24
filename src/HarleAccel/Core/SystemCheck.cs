// HarleAccel — Criado por Harle. (c) 2026.
// Pode modificar e redistribuir mantendo os creditos e deixando claro que e baseado no HarleAccel. Ver LICENSE.
// https://github.com/Harlezinhuu/HarleAccel

using System;
using System.Runtime.InteropServices;

namespace HarleAccel.Core
{
    /// <summary>Checagens do Windows que atrapalham quem usa aceleração.</summary>
    public static class SystemCheck
    {
        const uint SPI_GETMOUSE = 0x0003;
        const uint SPI_SETMOUSE = 0x0004;
        const uint SPIF_UPDATEINIFILE = 0x01;
        const uint SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", SetLastError = true)]
        static extern bool SystemParametersInfo(uint action, uint param, int[] vparam, uint winIni);

        /// <summary>"Aumentar precisão do ponteiro" (aceleração do próprio Windows) está ligada?</summary>
        public static bool? IsEnhancePointerPrecisionOn()
        {
            try
            {
                var m = new int[3];
                if (!SystemParametersInfo(SPI_GETMOUSE, 0, m, 0)) return null;
                return m[2] != 0;
            }
            catch { return null; }
        }

        /// <summary>Desliga a aceleração do Windows (não precisa de admin, vale só para o seu usuário).</summary>
        public static bool DisableEnhancePointerPrecision()
        {
            try
            {
                var m = new int[] { 0, 0, 0 };
                return SystemParametersInfo(SPI_SETMOUSE, 0, m, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
            }
            catch { return false; }
        }
    }
}
