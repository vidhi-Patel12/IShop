namespace ECommerce.Models
{
    public class ColorHelper
    {
        public static string GetHexColor(string colorName)
        {
            if (string.IsNullOrWhiteSpace(colorName))
                return "#CCCCCC"; // fallback color for null or empty names

            var color = System.Drawing.Color.FromName(colorName.Trim());

            // Check if it's a valid color (Alpha > 0)
            if (color.A > 0)
            {
                return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            }

            return "#CCCCCC"; // fallback color for unrecognized names
        }
    }
}