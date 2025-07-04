using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;


namespace TraducAmorous
{
    public partial class MainForm : Form
    {
        private string currentFilePath;
        private JObject jsonData;
        private List<TextItem> textItems = new List<TextItem>();

        public MainForm()
        {
            InitializeComponent();
            dataGridView.AutoGenerateColumns = false;
            dataGridView.CellEndEdit += DataGridView_CellEndEdit;
            this.Resize += MainForm_Resize;

            // Configurar drag and drop
            this.AllowDrop = true;
            this.DragEnter += MainForm_DragEnter;
            this.DragDrop += MainForm_DragDrop;
            this.DragLeave += MainForm_DragLeave; // Nuevo evento

            // Inicializar botones deshabilitados
            btnSave.Enabled = false;
            btnSaveAs.Enabled = false;

            this.Text = "Amorous - Editor de Textos V" + ProductVersion +" Alpha";

        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            // Verificar si el objeto arrastrado es un archivo
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);

                // Verificar si es un archivo JSON (aunque permitiremos cualquier archivo para flexibilidad)
                if (files.Length == 1)
                {
                    e.Effect = DragDropEffects.Copy;
                    return;
                }
            }
            e.Effect = DragDropEffects.None;
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length == 1)
            {
                currentFilePath = files[0];
                LoadJsonFile(currentFilePath);
            }
        }

        private void MainForm_DragLeave(object sender, EventArgs e)
        {
            // Restaurar el cursor cuando el archivo sale del área del formulario
            this.Cursor = Cursors.Default;
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (dataGridView.Columns.Count > 0)
            {
                int totalWidth = dataGridView.ClientSize.Width;
                if (totalWidth > 0)
                {
                    dataGridView.Columns[0].Width = (int)(totalWidth * 0.2);
                    dataGridView.Columns[1].Width = (int)(totalWidth * 0.4);
                    dataGridView.Columns[2].Width = (int)(totalWidth * 0.4);
                }
            }
        }


        private void btnOpen_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                openFileDialog.RestoreDirectory = true;

                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = openFileDialog.FileName;
                    LoadJsonFile(currentFilePath);
                }
            }
        }

        private void LoadJsonFile(string filePath)
        {
            try
            {
                this.Cursor = Cursors.WaitCursor;
                lblStatus.Text = "Cargando archivo...";
                Application.DoEvents(); // Para que se actualice el label inmediatamente

                // Verificar que el archivo existe y no está vacío
                if (!File.Exists(filePath))
                {
                    lblStatus.Text = "Archivo no encontrado";
                    MessageBox.Show("El archivo no existe.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                    return;
                }

                FileInfo fileInfo = new FileInfo(filePath);
                if (fileInfo.Length == 0)
                {
                    lblStatus.Text = "Archivo vacío";
                    MessageBox.Show("El archivo está vacío.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error); 
                    return;
                }

                string jsonContent = File.ReadAllText(filePath);
                jsonData = JObject.Parse(jsonContent);
                textItems.Clear();

                // Buscar recursivamente todos los campos "Text" en el JSON
                FindTextFields(jsonData);

                // Mostrar en la grilla
                dataGridView.DataSource = null;
                dataGridView.DataSource = textItems;
                lblStatus.Text = $"Archivo cargado: {Path.GetFileName(filePath)} - {textItems.Count} textos encontrados";
               
                // Habilitar botones relacionados
                btnSave.Enabled = true;
                btnSaveAs.Enabled = true;

            }
            catch (JsonException jex)
            {
                lblStatus.Text = "Error en formato JSON";
                MessageBox.Show($"Error en el formato JSON: {jex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Error al cargar archivo";
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                this.Cursor = Cursors.Default;
            }
        }

        private void FindTextFields(JToken token)
        {
            if (token.Type == JTokenType.Object)
            {
                foreach (JProperty property in token.Children<JProperty>())
                {
                    if (property.Name == "Text" && property.Value.Type == JTokenType.String)
                    {
                        // Obtener la ruta completa para poder actualizar después
                        string path = property.Path;
                        string text = property.Value.ToString();
                        textItems.Add(new TextItem { Path = path, OriginalText = text, TranslatedText = text });
                    }
                    else
                    {
                        FindTextFields(property.Value);
                    }
                }
            }
            else if (token.Type == JTokenType.Array)
            {
                foreach (JToken child in token.Children())
                {
                    FindTextFields(child);
                }
            }
        }

        private void DataGridView_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == 2 && e.RowIndex >= 0) // Columna de texto traducido
            {
                var editedItem = textItems[e.RowIndex];
                UpdateJsonValue(editedItem.Path, editedItem.TranslatedText);
            }
        }

        private void UpdateJsonValue(string path, string newValue)
        {
            try
            {
                JToken token = jsonData.SelectToken(path);
                if (token != null)
                {
                    token.Replace(newValue);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al actualizar el valor: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentFilePath))
            {
                MessageBox.Show("No hay archivo cargado para guardar.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                File.WriteAllText(currentFilePath, jsonData.ToString(Formatting.Indented));
                MessageBox.Show("Archivo guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                lblStatus.Text = $"Archivo guardado: {Path.GetFileName(currentFilePath)}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al guardar el archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnSaveAs_Click(object sender, EventArgs e)
        {
            if (jsonData == null)
            {
                MessageBox.Show("No hay datos para guardar.", "Advertencia", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using (SaveFileDialog saveFileDialog = new SaveFileDialog())
            {
                saveFileDialog.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
                saveFileDialog.RestoreDirectory = true;

                if (saveFileDialog.ShowDialog() == DialogResult.OK)
                {
                    currentFilePath = saveFileDialog.FileName;
                    File.WriteAllText(currentFilePath, jsonData.ToString(Formatting.Indented));
                    MessageBox.Show("Archivo guardado exitosamente.", "Éxito", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = $"Archivo guardado como: {Path.GetFileName(currentFilePath)}";
                }
            }
        }



    public class TextItem
    {
        public string Path { get; set; }
        public string OriginalText { get; set; }
        public string TranslatedText { get; set; }
    }

    }
}