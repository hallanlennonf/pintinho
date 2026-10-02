using System.Drawing;

namespace Pintinho
{
    // Cores do design system "Pintinho". Manter igual ao tokens.json do design:
    // light/dark = modo Infantil, cozy-light/cozy-dark = modo Aconchego.
    class Theme
    {
        public Color Surface, Panel, Ink, Muted, Borda, Taxi, OnTaxi, Cone, Bombeiro, Ceu, Trator, OnTrator, Veil, SwatchEdge, Dim;

        public static readonly Theme Claro = new Theme
        {
            Surface = Shapes.Hex(0xfff4dc), Panel = Shapes.Hex(0xffffff), Ink = Shapes.Hex(0x2b2118),
            Muted = Shapes.Hex(0x6e5f4e), Borda = Shapes.Hex(0xe3cfa8), Taxi = Shapes.Hex(0xffc727),
            OnTaxi = Shapes.Hex(0x2b2118), Cone = Shapes.Hex(0xe8680c), Bombeiro = Shapes.Hex(0xd93a26),
            Ceu = Shapes.Hex(0x2f8fc7), Trator = Shapes.Hex(0x3e9b4f), OnTrator = Color.White, Veil = Shapes.Hex(0x3b4048),
            SwatchEdge = Color.FromArgb(64, 43, 33, 24), Dim = Color.FromArgb(191, 59, 64, 72)
        };

        public static readonly Theme Escuro = new Theme
        {
            Surface = Shapes.Hex(0x1c1f24), Panel = Shapes.Hex(0x2a2f37), Ink = Shapes.Hex(0xf5ecdc),
            Muted = Shapes.Hex(0xb8ab98), Borda = Shapes.Hex(0x444b55), Taxi = Shapes.Hex(0xffc727),
            OnTaxi = Shapes.Hex(0x2b2118), Cone = Shapes.Hex(0xff8a3d), Bombeiro = Shapes.Hex(0xe5533e),
            Ceu = Shapes.Hex(0x5cb8ec), Trator = Shapes.Hex(0x5cbf6d), OnTrator = Shapes.Hex(0x1c1f24), Veil = Shapes.Hex(0x0e1013),
            SwatchEdge = Color.FromArgb(90, 245, 236, 220), Dim = Color.FromArgb(204, 14, 16, 19)
        };

        public static readonly Theme AconchegoClaro = new Theme
        {
            Surface = Shapes.Hex(0xf6f1ea), Panel = Shapes.Hex(0xfffdf9), Ink = Shapes.Hex(0x3a3330),
            Muted = Shapes.Hex(0x76695f), Borda = Shapes.Hex(0xe6dccf), Taxi = Shapes.Hex(0xf7d9c4),
            OnTaxi = Shapes.Hex(0x3a3330), Cone = Shapes.Hex(0xc8714b), Bombeiro = Shapes.Hex(0xc24e3b),
            Ceu = Shapes.Hex(0x4f82ad), Trator = Shapes.Hex(0x4f7a55), OnTrator = Color.White, Veil = Shapes.Hex(0x3a3330),
            SwatchEdge = Color.FromArgb(46, 58, 51, 48), Dim = Color.FromArgb(115, 58, 51, 48)
        };

        public static readonly Theme AconchegoEscuro = new Theme
        {
            Surface = Shapes.Hex(0x23201f), Panel = Shapes.Hex(0x2f2b29), Ink = Shapes.Hex(0xf3ece4),
            Muted = Shapes.Hex(0xb5aaa0), Borda = Shapes.Hex(0x48413d), Taxi = Shapes.Hex(0x8a5a44),
            OnTaxi = Shapes.Hex(0xf3ece4), Cone = Shapes.Hex(0xe59a76), Bombeiro = Shapes.Hex(0xe06a55),
            Ceu = Shapes.Hex(0x7fb0dc), Trator = Shapes.Hex(0x8fbf93), OnTrator = Shapes.Hex(0x23201f), Veil = Shapes.Hex(0x0f0e0d),
            SwatchEdge = Color.FromArgb(77, 243, 236, 228), Dim = Color.FromArgb(166, 15, 14, 13)
        };

        // Linha do desenho e papel: iguais em todos os temas.
        public static readonly Color Linha = Shapes.Hex(0x2b2118);
        public static readonly Color Papel = Color.White;

        public static readonly Color[] Palette = {
            Shapes.Hex(0x222222), Shapes.Hex(0xffffff), Shapes.Hex(0x9e9e9e), Shapes.Hex(0x8d5524),
            Shapes.Hex(0xe53935), Shapes.Hex(0xfb8c00), Shapes.Hex(0xfdd835), Shapes.Hex(0x9ccc65),
            Shapes.Hex(0x2e7d32), Shapes.Hex(0x26a69a), Shapes.Hex(0x4fc3f7), Shapes.Hex(0x1e63d6),
            Shapes.Hex(0x8e24aa), Shapes.Hex(0xf48fb1), Shapes.Hex(0xd81b60), Shapes.Hex(0xf1c27d)
        };

        // "Mais cores!" do Infantil
        public static readonly Color[] KidsExtra = {
            Shapes.Hex(0xffcdd2), Shapes.Hex(0xf8bbd0), Shapes.Hex(0xe1bee7), Shapes.Hex(0xd1c4e9), Shapes.Hex(0xc5cae9), Shapes.Hex(0xbbdefb),
            Shapes.Hex(0xb2ebf2), Shapes.Hex(0xc8e6c9), Shapes.Hex(0xdcedc8), Shapes.Hex(0xfff9c4), Shapes.Hex(0xffe0b2), Shapes.Hex(0xd7ccc8),
            Shapes.Hex(0xb71c1c), Shapes.Hex(0x880e4f), Shapes.Hex(0x4a148c), Shapes.Hex(0x1a237e), Shapes.Hex(0x01579b), Shapes.Hex(0x004d40),
            Shapes.Hex(0x1b5e20), Shapes.Hex(0x827717), Shapes.Hex(0xf57f17), Shapes.Hex(0xe65100), Shapes.Hex(0x3e2723), Shapes.Hex(0x607d8b)
        };

        // Paleta pastel do Aconchego
        public static readonly Color[] Cozy = {
            Shapes.Hex(0x2b2522), Shapes.Hex(0xffffff), Shapes.Hex(0x8c8c8c), Shapes.Hex(0xc9b8a8), Shapes.Hex(0xf4a7a3), Shapes.Hex(0xf7c6c7),
            Shapes.Hex(0xf9d5a7), Shapes.Hex(0xfbe7a1), Shapes.Hex(0xfff4c2), Shapes.Hex(0xd8e8b0), Shapes.Hex(0xb5d8b1), Shapes.Hex(0xa8d8cf),
            Shapes.Hex(0xa7d3e8), Shapes.Hex(0xb4c7f0), Shapes.Hex(0xc9b8ea), Shapes.Hex(0xe3b8e0), Shapes.Hex(0xe07a6f), Shapes.Hex(0xf0a35e),
            Shapes.Hex(0xe9c46a), Shapes.Hex(0x8fbf7a), Shapes.Hex(0x5fa89a), Shapes.Hex(0x6b9ac4), Shapes.Hex(0x8e7cc3), Shapes.Hex(0xc47aa6)
        };
    }
}
