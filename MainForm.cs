using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Security;
using System.Security.Cryptography;
using System.IO;
using System.Net;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Security.AccessControl;
using System.Threading;

namespace decrypter
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SystemParametersInfo(UInt32 action, UInt32 uParam, String vParam, UInt32 winIni);
        ulong decrypted = 0;

        public MainForm()
        {
            InitializeComponent();
        }

        public byte[] AES_Decrypt(byte[] bytesToBeDecrypted, byte[] passwordBytes, byte[] SaltBytes)
        {
            byte[] decryptedBytes = null;
            using (MemoryStream ms = new MemoryStream())
            {
                using (RijndaelManaged AES = new RijndaelManaged())
                {
                     
                    AES.KeySize = 256;
                    AES.BlockSize = 128;

                    var key = new Rfc2898DeriveBytes(passwordBytes, SaltBytes, 1000);
                    AES.Key = key.GetBytes(AES.KeySize / 8);
                    AES.IV = key.GetBytes(AES.BlockSize / 8);

                    AES.Mode = CipherMode.CBC;

                    using (var cs = new CryptoStream(ms, AES.CreateDecryptor(), CryptoStreamMode.Write))
                    {
                        cs.Write(bytesToBeDecrypted, 0, bytesToBeDecrypted.Length);
                        cs.Close();
                    }
                    decryptedBytes = ms.ToArray();
                }
            }

            return decryptedBytes;
        }

        public void DecryptFile(string file,string password, byte[] SaltBytes)
        {
            Console.WriteLine(password);
            byte[] BytesToBeDecrypted = File.ReadAllBytes(file);
            byte[] PasswordBytes = Encoding.UTF8.GetBytes(password);
            PasswordBytes = SHA256.Create().ComputeHash(PasswordBytes);

            byte[] BytesDecrypted = AES_Decrypt(BytesToBeDecrypted, PasswordBytes, SaltBytes);

            File.WriteAllBytes(file, BytesDecrypted);
            string result = file.Substring(0, file.Length - Path.GetExtension(file).Length);
            File.Move(file, result);
        }

        public void DecryptDirectory(string location, byte[] SaltBytes)
        {
            try
            {
                string password = textBox1.Text;

                string[] files = Directory.GetFiles(location);
                string[] childDirectories = Directory.GetDirectories(location);
                for (int i = 0; i < files.Length; i++)
                {
                    string extension = Path.GetExtension(files[i]);
                    if (extension == ".encrypted")
                    {
                        DecryptFile(files[i], password, SaltBytes);
                        decrypted++;
                    }
                }
                for (int i = 0; i < childDirectories.Length; i++)
                {
                    DecryptDirectory(childDirectories[i], SaltBytes);
                }
            }
            catch
            {

            }

        }
        string path = "";
        string BytesFile = "bytes.file";
        private void MainForm_Load(object sender, EventArgs e)
        {

            if (File.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), BytesFile)))
            {
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), BytesFile);
            }
            else if (File.Exists(BytesFile))
                path = BytesFile;
            else
            {
                MessageBox.Show($"Could not find {BytesFile}, please put the bytes file in the same folder as the software or on your desktop");
            }
            
        }

        private async void DecryptFilesButton_Click(object sender, EventArgs e)
        {
            byte[] SaltBytes = new byte[8];
            SaltBytes = Convert.FromBase64String(File.ReadAllText(path));

            DialogResult result = MessageBox.Show("Decrypting files... Please wait while doesn't appears Files decrypted and don't worry if software doesn't answear.", "Press ok to start decryption process.", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            if (result == DialogResult.Cancel)
            {
                Application.Exit();
            }

            timer1.Start();
            decryptedFilesLabel.ForeColor = Color.Green;
            progressBar1.Style = ProgressBarStyle.Marquee;
            Thread t1 = new Thread(() =>
            {
                List<Thread> Threads = new List<Thread>();
                foreach (DriveInfo d in DriveInfo.GetDrives())
                {
                    if (d.IsReady == true)
                    {
                        Threads.Add(new Thread(() =>
                        {
                            try { DecryptDirectory(d.Name, SaltBytes); } catch { }
                        }));
                    }
                }

                foreach (Thread t in Threads)
                    t.Start();
                foreach (Thread t in Threads)
                    t.Join();
            });
            t1.IsBackground = true;
            t1.Start();

            while (t1.IsAlive)
                await Task.Delay(1000);

            timer1.Stop();
            progressBar1.Style = ProgressBarStyle.Continuous;
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            decryptedFilesLabel.Text = $"Decrypted files: {decrypted}";
        }
    }
}
