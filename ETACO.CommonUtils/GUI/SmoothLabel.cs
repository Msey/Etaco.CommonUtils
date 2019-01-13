using System.Drawing.Text;
using System.Windows.Forms;

namespace ETACO.CommonUtils
{
    public partial class SmoothLabel : Label
    {
        private TextRenderingHint _hint = TextRenderingHint.SystemDefault;
        public TextRenderingHint TextRenderingHint
        {
            get { return _hint; }
            set { _hint = value; }
        }

        protected override void OnPaint(PaintEventArgs pe)
        {
            pe.Graphics.TextRenderingHint = TextRenderingHint;
            base.OnPaint(pe);
        }
    }
}
