using System;
using JetBrains.Annotations;

namespace BundlerMinifier
{
    sealed class ColoredTextRegion : IDisposable
    {
        readonly string _after;
        bool _isDisposed;

        ColoredTextRegion(Func<string, ColoredText> colorization)
        {
            if (StringExtensions.NoColor)
            {
                return;
            }

            string[] parts = colorization("|").ToString().Split('|');
            Console.Write(parts[0]);
            this._after = parts[1];
        }

        public static IDisposable Create(Func<string, ColoredText> colorization)
        {
            return new ColoredTextRegion(colorization);
        }

        public void Dispose()
        {
            if (this._isDisposed)
            {
                return;
            }

            this._isDisposed = true;

            if (!StringExtensions.NoColor)
            {
                Console.Write(this._after);
            }
        }
    }

    class ColoredText
    {
        int _color;
        readonly string _message;
        bool _bright;

        public ColoredText(string message)
        {
            this._message = message;
        }

        public ColoredText Bright()
        {
            this._bright = true;
            return this;
        }

        public ColoredText Red()
        {
            this._color = 31;
            return this;
        }

        public ColoredText Black()
        {
            this._color = 30;
            return this;
        }

        public ColoredText Green()
        {
            this._color = 32;
            return this;
        }

        public ColoredText Orange()
        {
            this._color = 33;
            return this;
        }

        public ColoredText Blue()
        {
            this._color = 34;
            return this;
        }

        public ColoredText Purple()
        {
            this._color = 35;
            return this;
        }

        public ColoredText Cyan()
        {
            this._color = 36;
            return this;
        }

        public ColoredText LightGray()
        {
            this._color = 37;
            return this;
        }

        public static implicit operator string(ColoredText t)
        {
            return t.ToString();
        }

        public override string ToString()
        {
            if(StringExtensions.NoColor || this._color == 0)
            {
                return this._message;
            }

            string colorString = this._color.ToString();
            if (this._bright)
            {
                colorString += "m\x1B[1";
            }

            return $"\x1B[{colorString}m{this._message}\x1B[0m\x1B[39m\x1B[49m";
        }
    }

    [UsedImplicitly(ImplicitUseTargetFlags.WithMembers)]
    static class StringExtensions
    {
        public static bool NoColor { get; set; }

        public static ColoredText Orange(this string s)
        {
            return new ColoredText(s).Orange();
        }

        public static ColoredText Black(this string s)
        {
            return new ColoredText(s).Black();
        }

        public static ColoredText Red(this string s)
        {
            return new ColoredText(s).Red();
        }

        public static ColoredText Green(this string s)
        {
            return new ColoredText(s).Green();
        }

        public static ColoredText Blue(this string s)
        {
            return new ColoredText(s).Blue();
        }

        public static ColoredText Purple(this string s)
        {
            return new ColoredText(s).Purple();
        }

        public static ColoredText Cyan(this string s)
        {
            return new ColoredText(s).Cyan();
        }

        public static ColoredText LightGray(this string s)
        {
            return new ColoredText(s).LightGray();
        }
    }
}
