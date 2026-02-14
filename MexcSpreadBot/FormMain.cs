namespace MexcSpreadBot
{
    public partial class FormMain : Form
    {
        public FormMain()
        {
            InitializeComponent();
        }

        private void спредыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Application.OpenForms.OfType<FormSpread>().Any())
            {
                var spreadForm = new FormSpread();
                spreadForm.Show();
            }
        }

        private void парыToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!Application.OpenForms.OfType<FormPairs>().Any())
            {
                var pairsForm = new FormPairs();
                pairsForm.Show();
            }
        }
    }
}
