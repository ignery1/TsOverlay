using System;
using System.Collections.Generic;
using System.Windows;
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Helpers
{
    // [新增] Replica o cálculo de posições fixas do MainViewModel.ApplyPositionPreset,
    // pra outras telas (Settings, Setup) saberem detectar/marcar qual preset está ativo
    // e converter entre coordenadas de tela real e o preview em miniatura.
    public static class PositionPresetHelper
    {
        public const double OverlayWidth = 300;
        public const double OverlayHeight = 600;
        public const double Margin = 20;
        private const double Tolerance = 2;

        public static Dictionary<string, (double X, double Y)> ComputeAll()
        {
            double screenW = SystemParameters.WorkArea.Width;
            double screenH = SystemParameters.WorkArea.Height;

            return new Dictionary<string, (double, double)>
            {
                ["TopLeft"] = (Margin, Margin),
                ["TopRight"] = (screenW - OverlayWidth - Margin, Margin),
                ["BottomLeft"] = (Margin, screenH - OverlayHeight - Margin),
                ["BottomRight"] = (screenW - OverlayWidth - Margin, screenH - OverlayHeight - Margin),
                ["CenterLeft"] = (Margin, (screenH - OverlayHeight) / 2),
                ["CenterRight"] = (screenW - OverlayWidth - Margin, (screenH - OverlayHeight) / 2),
            };
        }

        /// <summary>
        /// Nome do preset atual (ex: "TopLeft") ou null se a posição foi customizada
        /// (arrastada manualmente pra um lugar que não bate com nenhum preset).
        /// </summary>
        public static string? DetectCurrent(AppConfig config)
        {
            foreach (var kv in ComputeAll())
            {
                if (Math.Abs(config.WindowLeft - kv.Value.X) < Tolerance &&
                    Math.Abs(config.WindowTop - kv.Value.Y) < Tolerance)
                    return kv.Key;
            }
            return null;
        }
    }
}