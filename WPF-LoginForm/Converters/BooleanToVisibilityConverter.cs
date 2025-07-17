using System;
using System.Globalization;
using System.Windows; // Required for Visibility enum
using System.Windows.Data; // Required for IValueConverter

// Ensure this namespace matches your folder structure
namespace WPF_LoginForm.Converters
{
   
    [ValueConversion(typeof(bool), typeof(Visibility))]
    public class BooleanToVisibilityConverter : IValueConverter
    {
        /// <summary>
        /// Converts a boolean value to a Visibility value.
        /// </summary>
        /// <param name="value">The boolean value to convert.</param>
        /// <param name="targetType">Must be Visibility.</param>
        /// <param name="parameter">Optional parameter. Can use "Inverted" to reverse logic, or "Hidden" to use Visibility.Hidden instead of Collapsed.</param>
        /// <param name="culture">Culture information (not used).</param>
        /// <returns>Visibility.Visible if true (or false if inverted), otherwise Visibility.Collapsed (or Hidden).</returns>
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is bool))
            {
                return Visibility.Collapsed; // Default if not a boolean
            }

            bool boolValue = (bool)value;
            Visibility nonVisibleState = Visibility.Collapsed; // Default non-visible state

            // Check for parameter to use Hidden instead of Collapsed
            string paramString = parameter as string;
            if (paramString != null)
            {
                if (paramString.Equals("Hidden", StringComparison.OrdinalIgnoreCase))
                {
                    nonVisibleState = Visibility.Hidden;
                }
                // Check for parameter to invert the boolean logic
                if (paramString.Equals("Inverted", StringComparison.OrdinalIgnoreCase))
                {
                    boolValue = !boolValue;
                }
            }

            return boolValue ? Visibility.Visible : nonVisibleState;
        }

        /// <summary>
        /// Converts a Visibility value back to a boolean value.
        /// </summary>
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (!(value is Visibility))
            {
                return false; // Default if not Visibility
            }

            bool result = ((Visibility)value == Visibility.Visible);

            // Check for parameter to invert the boolean logic
            string paramString = parameter as string;
            if (paramString != null && paramString.Equals("Inverted", StringComparison.OrdinalIgnoreCase))
            {
                result = !result;
            }

            return result;
        }
    }
}