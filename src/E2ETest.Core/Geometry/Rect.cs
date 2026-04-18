namespace E2ETest.Core.Geometry
{
    /// <summary>TFM 독립 Rect. 모든 프레임워크에서 동일하게 사용되는 단순 POCO.</summary>
    public struct Rect
    {
        public int X;
        public int Y;
        public int Width;
        public int Height;

        public Rect(int x, int y, int width, int height) { X = x; Y = y; Width = width; Height = height; }

        public bool IsEmpty { get { return Width <= 0 || Height <= 0; } }
        public int CenterX { get { return X + Width / 2; } }
        public int CenterY { get { return Y + Height / 2; } }

        public static Rect Empty { get { return new Rect(0, 0, 0, 0); } }

        public override string ToString()
        {
            return "(" + X + "," + Y + " " + Width + "x" + Height + ")";
        }
    }
}
