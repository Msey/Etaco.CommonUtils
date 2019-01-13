using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml.XPath;

namespace ETACO.CommonUtils
{
    //form.Controls.Add(new SplitLayoutControl<Panel>(false, v => v.BorderStyle = BorderStyle.Fixed3D) { Dock = DockStyle.Fill });
    public class SplitLayoutControl<T> : Control where T : Control, new()
    {
        private const int MIN_REPOSITION_BOUND = 100;
        private Action<T> OnPrepareControl;
        
        public enum SplitState { None, Bottom, Top, Left, Right };
        private SplitState state = SplitState.None;
        
        private readonly Cursor splitCursor = new Cursor(Properties.Resources.cut.GetHicon());

        internal readonly List<Splitter<T>> verticalSplitters = new List<Splitter<T>>();
        internal readonly List<Splitter<T>> horizontalSplitters = new List<Splitter<T>>();

        private T _activeContent = null;
        private Splitter<T> activeSplitter = null;
        private int oldHeight;
        private int oldWidth;

        public bool Locked { get; set; }
        public event Action<T> OnActiveContentChanged;
        ////////////////////////////////////////////////////////////////////////////////////////////////////
        public SplitLayoutControl(bool locked = false, Action<T> onPrepareControl = null)
        {
            MinimumSize = new Size(10, 10);
            oldHeight = Height;
            oldWidth = Width;
            AddGUIListeners();
            OnPrepareControl = onPrepareControl;
            InitContent();
            Locked = locked;
        }

        public void InitContent()
        {
            Clear();
            ActiveContent = AddContent();
            ActiveContent.SetBounds(Splitter<T>.SIZE, Splitter<T>.SIZE, Width - (2 * Splitter<T>.SIZE), Height - (2 * Splitter<T>.SIZE));
        }

        public T ActiveContent
        {
            get { return _activeContent; }
            set
            {
                if (value != null && value != this)
                {
                    if (_activeContent != null) _activeContent.Paint -= ContentBorderPaint;
                    _activeContent = value;
                    _activeContent.Paint += ContentBorderPaint;
                    Refresh();
                    OnActiveContentChanged?.Invoke(_activeContent.Tag as T);
                }
            }
        }

        private void ContentBorderPaint(object s, PaintEventArgs e)
        {
            if (Controls.Count == 1) return;
            var c = (T)s;
            ControlPaint.DrawBorder(e.Graphics, new Rectangle(1, 1, c.ClientRectangle.Width - 2, c.ClientRectangle.Height - 2), //DodgerBlue
                    Color.LightSkyBlue, 2, ButtonBorderStyle.Solid,
                    Color.LightSkyBlue, 2, ButtonBorderStyle.Solid,
                    Color.LightSkyBlue, 2, ButtonBorderStyle.Solid,
                    Color.LightSkyBlue, 2, ButtonBorderStyle.Solid);
        }
  
        private void AddGUIListeners()
        {
            SizeChanged += (s, e) => { if ((Width >= MIN_REPOSITION_BOUND) && (Height >= MIN_REPOSITION_BOUND)) { Reposition(Width, Height); } };
            VisibleChanged += (s, e) => { if ((Width >= MIN_REPOSITION_BOUND) && (Height >= MIN_REPOSITION_BOUND)) { Reposition(Width, Height); } };
            MouseMove += (s, e) =>
            {
                if (Locked) return;
                if (e.Button == MouseButtons.Left)
                {
                    if (activeSplitter != null && activeSplitter.Move(e.X, e.Y)) Refresh();
                }
                else
                {
                    activeSplitter = horizontalSplitters.Find(h => h.Contains(e.X, e.Y));
                    if (activeSplitter == null) activeSplitter = verticalSplitters.Find(v => v.Contains(e.X, e.Y));
                    Cursor = activeSplitter == null ? Cursors.Default : activeSplitter.ResizingCursor;
                }
            };
            MouseDown += (s, e) => { if (!Locked && activeSplitter != null) activeSplitter.PrepareToMove();};
        }

        ////////////////////////////////////////   Resized   ///////////////////////////////////////////////
        internal void Reposition(int newWidth, int newHeight)
        {
            if (oldWidth == newWidth && oldHeight == newHeight) return;
            var kH = newHeight / (float)oldHeight;
            var kW = newWidth / (float)oldWidth;
            if (kW <= 1)
            {
                verticalSplitters.Sort((s1, s2) => s1.Pos - s2.Pos);
                verticalSplitters.ForEach(s => s.DoResize(kW));
                StretchToRight(oldWidth, newWidth);
            }
            else
            {
                StretchToRight(oldWidth, newWidth);
                verticalSplitters.Sort((s1, s2) => s2.Pos - s1.Pos);
                verticalSplitters.ForEach(s => s.DoResize(kW));
            }
            if (kH <= 1)
            {
                horizontalSplitters.Sort((s1, s2) => s1.Pos - s2.Pos);
                horizontalSplitters.ForEach(s => s.DoResize(kH));
                StretchToBottom(oldHeight, newHeight);
            }
            else
            {
                StretchToBottom(oldHeight, newHeight);
                horizontalSplitters.Sort((s1, s2) => s2.Pos - s1.Pos);
                horizontalSplitters.ForEach(s => s.DoResize(kH));
            }
            oldWidth = newWidth;
            oldHeight = newHeight;
            Refresh();
        }

        private void StretchToRight(int oldWidth, int newWidth)
        {
            foreach (Control c in Controls) if ((c.Bounds.X + c.Width) >= (oldWidth - Splitter<T>.SIZE)) c.Width = newWidth - c.Bounds.X - Splitter<T>.SIZE;        
            foreach (var s in horizontalSplitters) if (s.Stop == (oldWidth - Splitter<T>.SIZE)) s.Stop = newWidth - Splitter<T>.SIZE;  
        }

        private void StretchToBottom(int oldHeight, int newHeight)
        {
            foreach (Control c in Controls) if ((c.Bounds.Y + c.Height) >= (oldHeight - Splitter<T>.SIZE)) c.Height = newHeight - c.Bounds.Y - Splitter<T>.SIZE;
            foreach (var s in verticalSplitters) if (s.Stop == (oldHeight - Splitter<T>.SIZE)) s.Stop = newHeight - Splitter<T>.SIZE;
        }

        ////////////////////////////// Get status, Move splitter, Create splitter //////////////////////////
        private SplitState FixState()
        {
            var p = PointToClient(MousePosition);
            var c = GetChildAtPoint(p);
            if (c != null && c != this)
            {
                if ((c.Width > 2 * Splitter<T>.RANGE) && (c.Height > 2 * Splitter<T>.RANGE))
                {
                    if ((p.X - c.Left) <= Splitter<T>.RANGE) return SplitState.Left;
                    if ((c.Right - p.X) <= Splitter<T>.RANGE) return SplitState.Right;
                    if ((p.Y - c.Top) <= Splitter<T>.RANGE) return SplitState.Top;
                    if ((c.Bottom - p.Y) <= Splitter<T>.RANGE)  return SplitState.Bottom;
                }
            }
            return SplitState.None;
        }

        public void CreateSplitter(Point p, SplitState splitMode)
        {
            p = PointToClient(p);
            var c = GetChildAtPoint(p);
            if (c == null) return;
            switch (splitMode)
            {
                case (SplitState.Bottom):
                    verticalSplitters.Add(VerticalSplitter<T>.Split(this, c, p.X, Splitter<T>.NEW_TO_LEFT));
                    break;
                case (SplitState.Top):
                    verticalSplitters.Add(VerticalSplitter<T>.Split(this, c, p.X, Splitter<T>.NEW_TO_RIGHT));
                    break;
                case (SplitState.Left):
                    horizontalSplitters.Add(HorizontalSplitter<T>.split(this, c, p.Y, Splitter<T>.NEW_TO_RIGHT));
                    break;
                case (SplitState.Right):
                    horizontalSplitters.Add(HorizontalSplitter<T>.split(this, c, p.Y, Splitter<T>.NEW_TO_LEFT));
                    break;
            }
        }

        /////////////////////////////////////   Work with contents   ///////////////////////////////////////
        public void SetNextActive()
        {
            if (Controls.Count > 0)
            {
                T first = null;
                bool flag = false;
                foreach (T c in from c in Controls.Cast<Control>() orderby c.Bounds.Y, c.Bounds.X select c)
                {
                    if (first == null) first = c;
                    if (flag)
                    {
                        ActiveContent = c;
                        return;
                    }
                    flag = (ActiveContent == c);
                }
                ActiveContent = first;
            }
        }
        //////////////////////////////////////   Any   /////////////////////////////////////////////////////
        internal void GarbageCollector(Splitter<T> splitter)
        {
            horizontalSplitters.Remove(splitter);
            verticalSplitters.Remove(splitter);
            if (activeSplitter == splitter) activeSplitter = null;

            verticalSplitters.RemoveAll(s => s.Start > s.Stop);
            horizontalSplitters.RemoveAll(s => s.Start > s.Stop);

            for (var i = Controls.Count - 1; i >= 0; i--) { var c = Controls[i]; if (c.Height <= 0 || c.Width <= 0) { Controls.Remove(c); c.Dispose(); } }
            
            if (!Contains(_activeContent)) SetNextActive();
        }

        private void ContentMouseMove(object sender, MouseEventArgs e)
        {
            state = (!Locked && ModifierKeys == Keys.Control) ? FixState() : SplitState.None;
            Cursor = state == SplitState.None ? Cursors.Default : splitCursor;
        }
        private void ContentMouseDown(object sender, MouseEventArgs e)
        {
            if (state == SplitState.None) ActiveContent = (T)GetChildAtPoint(PointToClient(MousePosition));
            else if(!Locked)
            {
                CreateSplitter(MousePosition, state);
                Refresh();
            }
        }
        
      

        ////////////////////////////////////   add/remove/replace   /////////////////////////////////////////////////
        public Control Replace(T newContent)
        {
            return Replace(newContent, ActiveContent);  
        }
        
        public Control Replace(T newContent, T oldContent)
        {
            newContent.SetBounds(oldContent.Bounds);
            bool isActive = (oldContent == ActiveContent);
            Controls.Remove(oldContent);
            oldContent.Dispose();
            var ctrl = AddContent(newContent);
            if (isActive) ActiveContent = ctrl;
            return newContent;
        }

        public void Clear()
        {
            horizontalSplitters.Clear();
            verticalSplitters.Clear();
            for (var i = Controls.Count - 1; i >= 0; i--) { var c = Controls[i]; Controls.Remove(c); c.Dispose(); }
            _activeContent = null;
        }

        internal T AddContent(T content = null)
        {
            content = content ?? new T();
            content.Padding = new Padding(3);
            OnPrepareControl?.Invoke(content);
            PrepareContent(content);
            Controls.Add(content);
            return content;
        }

        private void PrepareContent(Control c)
        {
            c.MouseMove -= ContentMouseMove;
            c.MouseDown -= ContentMouseDown;
            c.MouseMove += ContentMouseMove;
            c.MouseDown += ContentMouseDown;
            foreach (Control child in c.Controls) PrepareContent(child);
        }

        /////////////////////////////////////////////////////////////////////////////
        public void Load(Func<string, T> getContent, MemoryStream ms)
        {
            int width = Width;
            int height = Height;
            Clear();

            var v = jss.ReadObject(ms);
            oldHeight = v.h;
            oldWidth = v.w;
            foreach(var c in v.contents)
            {
                var cc = getContent(c.name);
                cc.Visible = false;
                cc.SetBounds(c.x, c.y, c.w, c.h);
                AddContent(cc);
                cc.Visible = true;//не уверен, что нужно, вроде, убирает лишнее мерцание
            }
            foreach (var s in v.splitters)
            {
                if(s.type == "v") verticalSplitters.Add(new VerticalSplitter<T>(this, s.pos, s.from, s.to));
                else horizontalSplitters.Add(new HorizontalSplitter<T>(this, s.pos, s.from, s.to));
            }
            
            verticalSplitters.ForEach(s => s.PrepareToMove());
            horizontalSplitters.ForEach(s => s.PrepareToMove());
            Reposition(width, height);
            SetNextActive();
        }

        public MemoryStream Save()
        {
            return jss.ToStream(new SplitLayoutInfo()
            {
                contents = Controls.Cast<Control>().Select(c => new ContentInfo() { name = c.Name, x = c.Bounds.X, y = c.Bounds.Y, h = c.Bounds.Height, w = c.Bounds.Width }),
                splitters = verticalSplitters.Select(v => new SplitterInfo() { pos = v.Pos, from = v.Start, to = v.Stop, type = "v" }).Concat(
                     horizontalSplitters.Select(v => new SplitterInfo() { pos = v.Pos, from = v.Start, to = v.Stop, type = "h" })),
                w = Width,
                h = Height
            });
        }

        private Json.JsonSerializer<SplitLayoutInfo> jss = new Json.JsonSerializer<SplitLayoutInfo>();
        public class ContentInfo { public string name; public int x; public int y; public int w; public int h; }
        public class SplitterInfo { public string type; public int from; public int to; public int pos; }
        public class SplitLayoutInfo { public IEnumerable<ContentInfo> contents; public IEnumerable<SplitterInfo> splitters; public int w; public int h; }
    }

    internal abstract class Splitter<T> where T : Control, new()
    {
        internal const int SIZE = 1;
        internal const int RANGE = 3;
        internal const bool NEW_TO_LEFT = true;
        internal const bool NEW_TO_RIGHT = false;
        internal const int MIN_PANEL_SIZE = 25;

        private Splitter<T> leftBound = null;
        private Splitter<T> rightBound = null;
        private bool canMove = true;

        internal int Start { get; set; }
        internal int Stop { get; set; }
        internal int Pos { get; set; }
        internal int Left { get { return Pos - SIZE; } }
        internal int Right { get { return Pos + SIZE; } }
        internal int MinPos { get { return 0; } }
        internal SplitLayoutControl<T> OwnerControl { get; private set; }

        private readonly List<Splitter<T>> leftSplitters = new List<Splitter<T>>();
        private readonly List<Splitter<T>> rightSplitters = new List<Splitter<T>>();

        protected readonly List<Control> leftContents = new List<Control>();
        protected readonly List<Control> rightContents = new List<Control>();
          
        internal Splitter(SplitLayoutControl<T> ownerControl, int pos, int start, int stop)
        {
            OwnerControl = ownerControl;
            Pos = pos;
            Start = start;
            Stop = stop;
        }

        protected void Reinit()
        {
            FixContents();
            FixNeighbor();
        }

        internal void PrepareToMove()
        {
            Reinit();
            FixLeftRightSplitters();
            canMove = true;
        }

        private void FixContents()
        {
            leftContents.Clear();
            rightContents.Clear();
            foreach (Control control in OwnerControl.Controls)
            {
                if (IsLeftContent(control))
                {
                    leftContents.Add(control);
                }
                else if (IsRightContent(control))
                {
                    rightContents.Add(control);
                }
            }
        }

        private void FixLeftRightSplitters()
        {
            leftBound = null;
            rightBound = null;
            int left = MinPos;
            int right = MaxPos;
            foreach (var split in GetParallel())
            {
                if ((split.Pos > left) && (split.Pos < Pos) && (split.Start < Stop) && (split.Stop > Start))
                {
                    leftBound = split;
                    left = leftBound.Pos;
                }
                if ((split.Pos < right) && (split.Pos > Pos) && (split.Start < Stop) && (split.Stop > Start))
                {
                    rightBound = split;
                    right = rightBound.Pos;
                }
            }
        }

        private void FixNeighbor()
        {
            leftSplitters.Clear();
            rightSplitters.Clear();
            foreach (var split in GetOrtogonal())
            {
                if (split.Stop == Left)
                {
                    if ((split.Pos > Start) && (split.Pos < Stop))
                    {
                        leftSplitters.Add(split);
                    }
                }
                else if (split.Start == Right)
                {
                    if ((split.Pos > Start) && (split.Pos < Stop))
                    {
                        rightSplitters.Add(split);
                    }
                }
            }
        }

        ////////////////////////////////////////////   Moving   ////////////////////////////////////////////
        internal bool Move(int x, int y)
        {
            if (canMove)
            {
                int newPos = GetPosFromPoint(x, y);
                if (Pos != newPos)
                {
                    if ((rightBound != null) && (newPos > (rightBound.Pos - MIN_PANEL_SIZE)))       RightJoinSplitters();
                    else if ((leftBound != null) && (newPos < (leftBound.Pos + MIN_PANEL_SIZE)))    LeftJoinSplitters();
                    else if (newPos < (MinPos + MIN_PANEL_SIZE))                                    JoinSplitterWithMinPos();
                    else if (newPos > (MaxPos - MIN_PANEL_SIZE))                                    JoinSplitterWithMaxPos();
                    else UncheckedMove(newPos);
                    return true;
                }
            }
            return false;
        }

        protected void UncheckedMove(int newPos)
        {
            MoveContents(newPos - Pos);
            Pos = newPos;
            MoveSplitters();
        }

        private void JoinSplitterWithMinPos()
        {
            canMove = false;
            UncheckedMove(MinPos);
            OwnerControl.GarbageCollector(this);
        }

        private void JoinSplitterWithMaxPos()
        {
            canMove = false;
            UncheckedMove(MaxPos);
            OwnerControl.GarbageCollector(this);
        }

        private void MoveSplitters()
        {
            leftSplitters.ForEach(s => s.Stop = Left);
            rightSplitters.ForEach(s => s.Start = Right);
        }

        private void LeftJoinSplitters()
        {
            canMove = false;
            UncheckedMove(leftBound.Pos);
            int leftStart = leftBound.Start;
            int leftStop = leftBound.Stop;
            Start = (Start <= leftStart) ? Start : leftStart;
            Stop = (Stop >= leftStop) ? Stop : leftStop;
            OwnerControl.GarbageCollector(leftBound);
        }

        private void RightJoinSplitters()
        {
            canMove = false;
            UncheckedMove(rightBound.Pos);
            int rightStart = rightBound.Start;
            int rightStop = rightBound.Stop;
            Start = (Start <= rightStart) ? Start : rightStart;
            Stop = (Stop >= rightStop) ? Stop : rightStop;
            OwnerControl.GarbageCollector(rightBound);
        }
        ///////////////////////////////////////////////   Abstract   ///////////////////////////////////////
        internal abstract bool IsLeftContent(Control comp);
        internal abstract bool IsRightContent(Control comp);
        internal abstract bool Contains(int x, int y);
        internal abstract List<Splitter<T>> GetOrtogonal();
        internal abstract List<Splitter<T>> GetParallel();
        internal abstract int MaxPos { get; }
        internal abstract int GetPosFromPoint(int x, int y);
        internal abstract void MoveContents(int delta);
        internal abstract void DoResize(float quantity);
        internal abstract Cursor ResizingCursor { get; }
    }

    internal class HorizontalSplitter<T> : Splitter<T> where T : Control, new()
    {
        internal static HorizontalSplitter<T> split(SplitLayoutControl<T> ownerControl, Control content, int pos, bool isLeft)
        {
            int h = content.Height;
            Control left = null;
            Control right = null;
            if (isLeft)
            {
                left = ownerControl.AddContent();
                right = content;
            }
            else
            {
                left = content;
                right = ownerControl.AddContent();
            }
            try
            {
                GUIUtils.LockWindowUpdate(ownerControl.Handle);
                left.SetBounds(content.Bounds.X, content.Bounds.Y, content.Width, pos - content.Bounds.Y - SIZE);
                right.SetBounds(left.Bounds.X, pos + SIZE, left.Width, h - left.Height - (2 * SIZE));
            }
            finally
            {
                GUIUtils.LockWindowUpdate((IntPtr)0);
            }
            return (new HorizontalSplitter<T>(ownerControl, pos, left.Bounds.X, left.Bounds.X + left.Width));
        }

        internal HorizontalSplitter(SplitLayoutControl<T> ownerControl, int pos, int start, int stop) : base(ownerControl, pos, start, stop) { }

        internal override bool IsRightContent(Control comp)
        {
            return (comp.Bounds.Y == Right) && (comp.Bounds.X >= Start) && ((comp.Bounds.X + comp.Width) <= Stop);
        }

        internal override bool IsLeftContent(Control comp)
        {
            return ((comp.Bounds.Y + comp.Height) == Left) && (comp.Bounds.X >= Start) && ((comp.Bounds.X + comp.Width) <= Stop);
        }

        internal override int GetPosFromPoint(int x, int y)
        {
            return (x < Start && x > Stop) ? Pos : y;
        }

        internal override int MaxPos { get { return OwnerControl.Height; } }

        internal override bool Contains(int x, int y)
        {
            return (y >= Left) && (y <= Right) && (x > Start) && (x < Stop);
        }

        internal override List<Splitter<T>> GetOrtogonal()
        {
            return OwnerControl.verticalSplitters;
        }

        internal override List<Splitter<T>> GetParallel()
        {
            return OwnerControl.horizontalSplitters;
        }

        internal override void DoResize(float quantity)
        {
            Reinit();
            UncheckedMove((int)Math.Floor((Pos - SIZE) * quantity + SIZE));
        }

        internal override void MoveContents(int delta)
        {
            leftContents.ForEach(c => c.Height = c.Height + delta);
            try
            {
                GUIUtils.LockWindowUpdate(OwnerControl.Handle);
                rightContents.ForEach(c => c.SetBounds(c.Bounds.X, c.Bounds.Y + delta, c.Width, c.Height - delta));
            }
            finally
            {
                GUIUtils.LockWindowUpdate((IntPtr)0);
            }
        }

        internal override Cursor ResizingCursor
        {
            get { return Cursors.HSplit; }
        }
    }

    internal class VerticalSplitter<T> : Splitter<T> where T : Control, new()
    {
        internal static VerticalSplitter<T> Split(SplitLayoutControl<T> ownerControl, Control content, int pos, bool isLeft)
        {
            int w = content.Width;
            Control left = null;
            Control right = null;
            if (isLeft)
            {
                left = ownerControl.AddContent();
                right = content;
            }
            else
            {
                left = content;
                right = ownerControl.AddContent();
            }
            try
            {
                GUIUtils.LockWindowUpdate(ownerControl.Handle);
                left.SetBounds(content.Bounds.X, content.Bounds.Y, pos - content.Bounds.X - SIZE, content.Height);
                right.SetBounds(pos + SIZE, left.Bounds.Y, w - left.Width - (2 * SIZE), left.Height);
            }
            finally
            {
                GUIUtils.LockWindowUpdate((IntPtr)0);
            }

            return (new VerticalSplitter<T>(ownerControl, pos, left.Bounds.Y, left.Bounds.Y + left.Height));
        }

        internal VerticalSplitter(SplitLayoutControl<T> ownerControl, int pos, int start, int stop) : base(ownerControl, pos, start, stop) { }

        internal override bool IsRightContent(Control comp)
        {
            return (comp.Bounds.X == Right) && (comp.Bounds.Y >= Start) && ((comp.Bounds.Y + comp.Height) <= Stop);
        }

        internal override bool IsLeftContent(Control comp)
        {
            return ((comp.Bounds.X + comp.Width) == Left) && (comp.Bounds.Y >= Start) && ((comp.Bounds.Y + comp.Height) <= Stop);
        }

        internal override int GetPosFromPoint(int x, int y)
        {
            return (y < Start && y > Stop) ? Pos : x;
        }

        internal override int MaxPos { get { return OwnerControl.Width; } }

        internal override bool Contains(int x, int y)
        {
            return (x >= Left) && (x <= Right) && (y > Start) && (y < Stop);
        }

        internal override List<Splitter<T>> GetOrtogonal()
        {
            return OwnerControl.horizontalSplitters;
        }

        internal override List<Splitter<T>> GetParallel()
        {
            return OwnerControl.verticalSplitters;
        }

        internal override void DoResize(float quantity)
        {
            Reinit();
            UncheckedMove((int)Math.Floor((Pos - SIZE) * quantity + SIZE));
        }

        internal override void MoveContents(int delta)
        {
            leftContents.ForEach(c => c.Width = c.Width + delta);
            try
            {
                GUIUtils.LockWindowUpdate(OwnerControl.Handle);
                rightContents.ForEach(c => c.SetBounds(c.Bounds.X + delta, c.Bounds.Y, c.Width - delta, c.Height));
            }
            finally
            {
                GUIUtils.LockWindowUpdate((IntPtr)0);
            }
        }

        internal override Cursor ResizingCursor
        {
            get { return Cursors.VSplit; }
        }
    }
}
