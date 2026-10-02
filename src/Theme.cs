using System.Drawing;

namespace Pintinho
{
    // Cores do design system "Pintinho" (claro e escuro). Manter igual ao tokens.json do design.
    class Theme
    {
        public Color Surface, Panel, Ink, Muted, Borda, Taxi, OnTaxi, Cone, Bombeiro, Ceu, Trator, Veil, SwatchEdge;

        public static readonly Theme Claro = new Theme
        {
            Surface = Shapes.Hex(0xfff4dc), Panel = Shapes.Hex(0xffffff), Ink = Shapes.Hex(0x2b2118),
            Muted = Shapes.Hex(0x6e5f4e), Borda = Shapes.Hex(0xe3cfa8), Taxi = Shapes.Hex(0xffc727),
            OnTaxi = Shapes.Hex(0x2b2118), Cone = Shapes.Hex(0xe8680c), Bombeiro = Shapes.Hex(0xd93a26),
            Ceu = Shapes.Hex(0x2f8fc7), Trator = Shapes.Hex(0x3e9b4f), Veil = Shapes.Hex(0x3b4048),
            SwatchEdge = Color.FromArgb(64, 43, 33, 24)
        };

        public static readonly Theme Escuro = new Theme
        {
            Surface = Shapes.Hex(0x1c1f24), Panel = Shapes.Hex(0x2a2f37), Ink = Shapes.Hex(0xf5ecdc),
            Muted = Shapes.Hex(0xb8ab98), Borda = Shapes.Hex(0x444b55), Taxi = Shapes.Hex(0xffc727),
            OnTaxi = Shapes.Hex(0x2b2118), Cone = Shapes.Hex(0xff8a3d), Bombeiro = Shapes.Hex(0xe5533e),
            Ceu = Shapes.Hex(0x5cb8ec), Trator = Shapes.Hex(0x5cbf6d), Veil = Shapes.Hex(0x0e1013),
            SwatchEdge = Color.FromArgb(90, 245, 236, 220)
        };

        // Linha do desenho e papel: iguais nos dois temas.
        public static readonly Color Linha = Shapes.Hex(0x2b2118);
        public static readonly Color Papel = Color.White;

        public static readonly Color[] Palette = {
            Shapes.Hex(0x222222), Shapes.Hex(0xffffff), Shapes.Hex(0x9e9e9e), Shapes.Hex(0x8d5524),
            Shapes.Hex(0xe53935), Shapes.Hex(0xfb8c00), Shapes.Hex(0xfdd835), Shapes.Hex(0x9ccc65),
            Shapes.Hex(0x2e7d32), Shapes.Hex(0x26a69a), Shapes.Hex(0x4fc3f7), Shapes.Hex(0x1e63d6),
            Shapes.Hex(0x8e24aa), Shapes.Hex(0xf48fb1), Shapes.Hex(0xd81b60), Shapes.Hex(0xf1c27d)
        };
    }
}
