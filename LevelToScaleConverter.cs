using System.Globalization;
using System.Windows.Data;

namespace BF_STT
{
    public class LevelToScaleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            float level = 0;
            if (value is float f) level = f;
            else if (value is double d) level = (float)d;

            float multiplier = 1.0f;
            if (parameter != null && float.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float p))
            {
                multiplier = p;
            }

            // Return a scale where 1.0 is the minimum and it grows based on level
            // Level is 0 to 1. We want to scale Y from 0.2 to 2.0 maybe?
            // Actually, internal bars can be small when quiet and large when loud.
            return 0.1 + (level * multiplier * 2.0);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
