using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace Gui.Converters
{
    public class StatusColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isTranslated)
            {
                return isTranslated
                    ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(76, 175, 80))  // 绿色 - 已翻译
                    : new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)); // 橙色 - 待翻译
            }
            return System.Windows.Media.Brushes.Gray;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
