﻿using System;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace WordDetector
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void MainForm_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Copy;
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void MainForm_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                foreach (string path in (string[])e.Data.GetData(DataFormats.FileDrop))
                {
                    TreeNode node = new TreeNode(path);

                    FileTreeView.Nodes.Add(node);

                    if (Directory.Exists(path))
                    {
                        foreach (string filePath in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                        {
                            node.Nodes.Add(filePath);
                        }
                    }
                }
            }
        }

        private Regex regex;

        private void StartButton_Click(object sender, EventArgs e)
        {
            regex = new Regex(PatternTextBox.Text);

            foreach (TreeNode node in FileTreeView.Nodes)
            {
                if (node.Nodes.Count == 0)
                {
                    if (Search(node.Text))
                    {
                        node.BackColor = Color.YellowGreen;
                    }
                    else
                    {
                        node.BackColor = Color.White;
                    }
                }
                else
                {
                    foreach (TreeNode child in node.Nodes)
                    {
                        if (Search(child.Text))
                        {
                            child.BackColor = Color.YellowGreen;
                        }
                        else
                        {
                            child.BackColor = Color.White;
                        }
                    }

                    node.Expand();
                }
            }
        }

        private bool Search(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            Encoding encoding = null;

            encoding = DetectEncodingFromBOM(bytes);

            if (encoding == null)
            {
                encoding = DetectEncoding(bytes);
            }

            if (encoding == null)
            {
                return false;
            }
            else
            {
                return regex.IsMatch(encoding.GetString(bytes));
            }
        }

        private Encoding DetectEncodingFromBOM(byte[] bytes)
        {
            if (bytes.Length < 2)
            {
                return null;
            }
            if ((bytes[0] == 0xfe) && (bytes[1] == 0xff))
            {
                //UTF-16 BE
                return new System.Text.UnicodeEncoding(true, true);
            }
            if ((bytes[0] == 0xff) && (bytes[1] == 0xfe))
            {
                if ((4 <= bytes.Length) &&
                    (bytes[2] == 0x00) && (bytes[3] == 0x00))
                {
                    //UTF-32 LE
                    return new System.Text.UTF32Encoding(false, true);
                }
                //UTF-16 LE
                return new System.Text.UnicodeEncoding(false, true);
            }
            if (bytes.Length < 3)
            {
                return null;
            }
            if ((bytes[0] == 0xef) && (bytes[1] == 0xbb) && (bytes[2] == 0xbf))
            {
                //UTF-8
                return new System.Text.UTF8Encoding(true, true);
            }
            if (bytes.Length < 4)
            {
                return null;
            }
            if ((bytes[0] == 0x00) && (bytes[1] == 0x00) &&
                (bytes[2] == 0xfe) && (bytes[3] == 0xff))
            {
                //UTF-32 BE
                return new System.Text.UTF32Encoding(true, true);
            }

            return null;
        }

        private Encoding DetectEncoding(byte[] bytes)
        {
            const byte bEscape = 0x1B;
            const byte bAt = 0x40;
            const byte bDollar = 0x24;
            const byte bAnd = 0x26;
            const byte bOpen = 0x28;    //'('
            const byte bB = 0x42;
            const byte bD = 0x44;
            const byte bJ = 0x4A;
            const byte bI = 0x49;

            int len = bytes.Length;
            byte b1, b2, b3, b4;

            //Encode::is_utf8 は無視

            bool isBinary = false;
            for (int i = 0; i < len; i++)
            {
                b1 = bytes[i];
                if (b1 <= 0x06 || b1 == 0x7F || b1 == 0xFF)
                {
                    //'binary'
                    isBinary = true;
                    if (b1 == 0x00 && i < len - 1 && bytes[i + 1] <= 0x7F)
                    {
                        //smells like raw unicode
                        return System.Text.Encoding.Unicode;
                    }
                }
            }
            if (isBinary)
            {
                return null;
            }

            //not Japanese
            bool notJapanese = true;
            for (int i = 0; i < len; i++)
            {
                b1 = bytes[i];
                if (b1 == bEscape || 0x80 <= b1)
                {
                    notJapanese = false;
                    break;
                }
            }
            if (notJapanese)
            {
                return System.Text.Encoding.ASCII;
            }

            for (int i = 0; i < len - 2; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                b3 = bytes[i + 2];

                if (b1 == bEscape)
                {
                    if (b2 == bDollar && b3 == bAt)
                    {
                        //JIS_0208 1978
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bDollar && b3 == bB)
                    {
                        //JIS_0208 1983
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bOpen && (b3 == bB || b3 == bJ))
                    {
                        //JIS_ASC
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    else if (b2 == bOpen && b3 == bI)
                    {
                        //JIS_KANA
                        //JIS
                        return System.Text.Encoding.GetEncoding(50220);
                    }
                    if (i < len - 3)
                    {
                        b4 = bytes[i + 3];
                        if (b2 == bDollar && b3 == bOpen && b4 == bD)
                        {
                            //JIS_0212
                            //JIS
                            return System.Text.Encoding.GetEncoding(50220);
                        }
                        if (i < len - 5 &&
                            b2 == bAnd && b3 == bAt && b4 == bEscape &&
                            bytes[i + 4] == bDollar && bytes[i + 5] == bB)
                        {
                            //JIS_0208 1990
                            //JIS
                            return System.Text.Encoding.GetEncoding(50220);
                        }
                    }
                }
            }

            //should be euc|sjis|utf8
            //use of (?:) by Hiroki Ohzaki <ohzaki@iod.ricoh.co.jp>
            int sjis = 0;
            int euc = 0;
            int utf8 = 0;
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if (((0x81 <= b1 && b1 <= 0x9F) || (0xE0 <= b1 && b1 <= 0xFC)) &&
                    ((0x40 <= b2 && b2 <= 0x7E) || (0x80 <= b2 && b2 <= 0xFC)))
                {
                    //SJIS_C
                    sjis += 2;
                    i++;
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if (((0xA1 <= b1 && b1 <= 0xFE) && (0xA1 <= b2 && b2 <= 0xFE)) ||
                    (b1 == 0x8E && (0xA1 <= b2 && b2 <= 0xDF)))
                {
                    //EUC_C
                    //EUC_KANA
                    euc += 2;
                    i++;
                }
                else if (i < len - 2)
                {
                    b3 = bytes[i + 2];
                    if (b1 == 0x8F && (0xA1 <= b2 && b2 <= 0xFE) &&
                        (0xA1 <= b3 && b3 <= 0xFE))
                    {
                        //EUC_0212
                        euc += 3;
                        i += 2;
                    }
                }
            }
            for (int i = 0; i < len - 1; i++)
            {
                b1 = bytes[i];
                b2 = bytes[i + 1];
                if ((0xC0 <= b1 && b1 <= 0xDF) && (0x80 <= b2 && b2 <= 0xBF))
                {
                    //UTF8
                    utf8 += 2;
                    i++;
                }
                else if (i < len - 2)
                {
                    b3 = bytes[i + 2];
                    if ((0xE0 <= b1 && b1 <= 0xEF) && (0x80 <= b2 && b2 <= 0xBF) &&
                        (0x80 <= b3 && b3 <= 0xBF))
                    {
                        //UTF8
                        utf8 += 3;
                        i += 2;
                    }
                }
            }
            //M. Takahashi's suggestion
            //utf8 += utf8 / 2;

            System.Diagnostics.Debug.WriteLine(
                string.Format("sjis = {0}, euc = {1}, utf8 = {2}", sjis, euc, utf8));
            if (euc > sjis && euc > utf8)
            {
                //EUC
                return System.Text.Encoding.GetEncoding(51932);
            }
            else if (sjis > euc && sjis > utf8)
            {
                //SJIS
                return System.Text.Encoding.GetEncoding(932);
            }
            else if (utf8 > euc && utf8 > sjis)
            {
                //UTF8
                return System.Text.Encoding.UTF8;
            }

            return null;
        }

        private void FileTreeView_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo("explorer", "\"" + Directory.GetParent(e.Node.Text).FullName + "\"");
                process.Start();
            }
        }

        private void FileTreeView_NodeMouseDoubleClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            using (var process = new Process())
            {
                process.StartInfo = new ProcessStartInfo(e.Node.Text);
                process.Start();
            }
        }
    }
}
