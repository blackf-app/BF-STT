using Avalonia.Data.Converters;
using System.Globalization;

namespace BF_STT
{
    public class LevelToScaleConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            float level = 0;
            if (value is float f) level = f;
            else if (value is double d) level = (float)d;

            float multiplier = 1.0f;
            if (parameter != null && float.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out float p))
            {
                multiplier = p;
            }

            return 0.1 + (level * multiplier * 2.0);
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
