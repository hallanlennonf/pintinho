using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Pintinho
{
    // Entrada: cada dedo chega separado pelas mensagens WM_POINTER do Windows 8+.
    // O mouse continua chegando pelas mensagens normais (id -1).
    partial class MainForm
    {
        const int WM_POINTERUPDATE = 0x0245, WM_POINTERDOWN = 0x0246, WM_POINTERUP = 0x0247, WM_POINTERCAPTURECHANGED = 0x024C;
        const int POINTER_FLAG_INCONTACT = 0x4;
        const int PT_MOUSE = 4;
        const int MouseId = -1;

        [DllImport("user32.dll")]
        static extern bool GetPointerType(uint pointerId, out int pointerType);

        bool pointerApi = true;

        protected override void WndProc(ref Message m)
        {
            if (pointerApi && (m.Msg == WM_POINTERDOWN || m.Msg == WM_POINTERUPDATE || m.Msg == WM_POINTERUP || m.Msg == WM_POINTERCAPTURECHANGED))
            {
                if (HandlePointer(ref m)) return;
            }
            base.WndProc(ref m);
        }

        bool HandlePointer(ref Message m)
        {
            long w = m.WParam.ToInt64();
            int id = (int)(w & 0xFFFF);
            int flags = (int)((w >> 16) & 0xFFFF);
            try
            {
                int type;
                if (GetPointerType((uint)id, out type) && type == PT_MOUSE) return false; // mouse: deixa virar clique normal
            }
            catch (EntryPointNotFoundException)
            {
                pointerApi = false;
                return false;
            }

            long l = m.LParam.ToInt64();
            Point p = PointToClient(new Point((short)(l & 0xFFFF), (short)((l >> 16) & 0xFFFF)));
            try
            {
                if (m.Msg == WM_POINTERDOWN) PointerDown(id, p);
                else if (m.Msg == WM_POINTERUPDATE) { if ((flags & POINTER_FLAG_INCONTACT) != 0) PointerMove(id, p); }
                else PointerUp(id);
            }
            catch (Exception ex)
            {
                Program.Log(ex);
            }
            m.Result = IntPtr.Zero;
            return true;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left) PointerDown(MouseId, e.Location);
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if ((e.Button & MouseButtons.Left) != 0) PointerMove(MouseId, e.Location);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (e.Button == MouseButtons.Left) PointerUp(MouseId);
        }

        void PointerDown(int id, Point p)
        {
            if (kidsSurface == null) return;
            if (contacts.ContainsKey(id)) PointerUp(id);
            Contact c = new Contact();
            c.Id = id;
            c.Screen = p;
            c.DownAt = Environment.TickCount;
            contacts[id] = c;

            switch (overlay)
            {
                case Overlay.Galeria: GalleryDown(p); return;
                case Overlay.Pais: ParentDown(p); return;
                case Overlay.MaisCores: KidsColorsDown(p); return;
                case Overlay.Carimbos:
                    if (screen == Screen.Infantil) { KidsStampsDown(p); return; }
                    if (CozyStampsDown(c, p)) return;
                    break;
                case Overlay.Misturador:
                    if (MixerDown(c, p)) return;
                    break;
            }

            if (LockRect().Contains(p))
            {
                c.Role = Role.Cadeado;
                return;
            }
            switch (screen)
            {
                case Screen.Inicio: HomeDown(p); break;
                case Screen.Infantil: KidsDown(c, p); break;
                case Screen.Aconchego: CozyDown(c, p); break;
                case Screen.Jogo: GameDown(c, p); break;
                case Screen.Jogos: HubDown(p); break;
                case Screen.Cobra: SnakeDown(c, p); break;
            }
        }

        void PointerMove(int id, Point p)
        {
            Contact c;
            if (!contacts.TryGetValue(id, out c)) return;
            switch (c.Role)
            {
                case Role.Pintar:
                    if (screen == Screen.Infantil) KidsStroke(c, p);
                    else if (screen == Screen.Aconchego) CozyStroke(c, p);
                    break;
                case Role.Pinca:
                    c.Screen = p;
                    PinchMove();
                    break;
                case Role.Arrastar:
                    if (c.Drag != null) c.Drag(p);
                    break;
                case Role.Cadeado:
                    if (!Rectangle.Inflate(LockRect(), R(20), R(20)).Contains(p)) CancelLock(c);
                    break;
            }
            c.Screen = p;
        }

        void PointerUp(int id)
        {
            Contact c;
            if (!contacts.TryGetValue(id, out c)) return;
            contacts.Remove(id);
            if (screen == Screen.Cobra) SnakeUp(id);
            switch (c.Role)
            {
                case Role.Pintar:
                    if (screen == Screen.Aconchego) CozyEndStroke(c);
                    break;
                case Role.Pinca:
                    EndPinch();
                    break;
                case Role.Cadeado:
                    CancelLock(c);
                    break;
            }
        }

        void CancelLock(Contact c)
        {
            c.Role = Role.Nenhum;
            Invalidate(Rectangle.Inflate(LockRect(), R(10), R(10)));
        }
    }
}
