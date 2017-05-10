using System;

namespace KiCadDoxer.Renderer
{
    internal class TextSettings
    {
        private bool autoRotateUpsideDownText;
        private string classNames;
        private TextHorizontalJustify horizontalJustify;
        private bool isBold;
        private bool isItalic;
        private bool isWrittenToOutputAndLocked;
        private double size = 50;
        private string stroke = "rgb(0,0,0)";
        private double strokeWidth;
        private TextVerticalJustify verticalJustify;

        public bool AutoRotateUpsideDownText
        {
            get
            {
                return autoRotateUpsideDownText;
            }
            set
            {
                if (value != autoRotateUpsideDownText)
                {
                    AssertCanWrite();
                    this.autoRotateUpsideDownText = value;
                }
            }
        }

        public string ClassNames
        {
            get
            {
                return ClassNames;
            }
            set
            {
                if (value != classNames)
                {
                    AssertCanWrite();
                    this.classNames = value;
                }
            }
        }

        public TextHorizontalJustify HorizontalJustify
        {
            get
            {
                return horizontalJustify;
            }
            set
            {
                if (value != horizontalJustify)
                {
                    AssertCanWrite();
                    this.horizontalJustify = value;
                }
            }
        }

        public bool IsBold
        {
            get
            {
                return isBold;
            }
            set
            {
                if (value != isBold)
                {
                    AssertCanWrite();
                    this.isBold = value;
                }
            }
        }

        public bool IsItalic
        {
            get
            {
                return isItalic;
            }
            set
            {
                if (value != isItalic)
                {
                    AssertCanWrite();
                    this.isItalic = value;
                }
            }
        }

        public bool IsWrittenToOutputAndLocked
        {
            get
            {
                return isWrittenToOutputAndLocked;
            }
            set
            {
                if (value != isWrittenToOutputAndLocked)
                {
                    AssertCanWrite();
                    this.isWrittenToOutputAndLocked = value;
                }
            }
        }

        public double Size
        {
            get
            {
                return size;
            }
            set
            {
                if (size != value)
                {
                    AssertCanWrite();
                    this.size = value;
                }
            }
        }

        public string Stroke
        {
            get
            {
                return stroke;
            }
            set
            {
                if (stroke != value)
                {
                    AssertCanWrite();
                    this.stroke = value;
                }
            }
        }

        public double StrokeWidth
        {
            get
            {
                return strokeWidth;
            }
            set
            {
                if (strokeWidth != value)
                {
                    AssertCanWrite();
                    this.strokeWidth = value;
                }
            }
        }

        public TextVerticalJustify VerticalJustify
        {
            get
            {
                return verticalJustify;
            }
            set
            {
                if (value != verticalJustify)
                {
                    AssertCanWrite();
                    this.verticalJustify = value;
                }
            }
        }

        private void AssertCanWrite()
        {
            if (IsWrittenToOutputAndLocked)
            {
                throw new NotSupportedException("The TextSettings have been written to output and can no longer be modified.");
            }
        }
    }
}