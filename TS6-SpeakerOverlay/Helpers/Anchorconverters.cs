using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TS6_SpeakerOverlay.Helpers
{
    /// <summary>
    /// Converte Config.AnchorRight (bool) em HorizontalAlignment.
    /// true  -> Right (cards colados na borda direita da janela, útil quando a posição é Top/Center/Bottom Right)
    /// false -> Left  (comportamento padrão, como já era antes)
    /// </summary>
    public class BoolToHorizontalAlignmentConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool anchorRight = value is bool b && b;
            return anchorRight ? System.Windows.HorizontalAlignment.Right : System.Windows.HorizontalAlignment.Left;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}