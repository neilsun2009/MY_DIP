using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dipHW_4_task_3
{

    public partial class Form1 : Form
    {
        string path;
        private Bitmap img;
        byte[] srcData;
        byte[] RData;
        byte[] GData;
        byte[] BData;

        public Form1()
        {
            InitializeComponent();
        }

        // load and initialize from file
        private void LoadBitmap(string path) {
            // read from file
            img = (Bitmap)Image.FromFile(path);
            pictureBox1.Image = img;
            label1.Text = img.Width + "*" + img.Height;

            // read byte data
            BitmapData bitmapData = img.LockBits(new Rectangle(0, 0, img.Width, img.Height),
                ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            int bytes = img.Width * img.Height * 3;
            srcData = new byte[bytes];
            RData = new byte[bytes / 3];
            GData = new byte[bytes / 3];
            BData = new byte[bytes / 3];
            IntPtr srcPtr = bitmapData.Scan0;
            Marshal.Copy(srcPtr, srcData, 0, bytes);
            for (int i = 0, j = 0; i < bytes; i += 3,j++) {
                RData[j] = srcData[i];
                GData[j] = srcData[i + 1];
                BData[j] = srcData[i + 2];
            }
            // pay attention: order in byte array: height first
            img.UnlockBits(bitmapData);
        }

        // build a new bitmap with byte data
        private void BuildBitmap(int width, int height, byte[] newRData, byte[] newGData, byte[] newBData) {
            // generate new srcData
            int bytes = width * height * 3;
            byte[] newData = new byte[bytes];
            for (int i = 0, j = 0; i < bytes; i += 3, j++)
            {
                newData[i] = newRData[j];
                newData[i + 1] = newGData[j];
                newData[i + 2] = newBData[j];
            }

            // pay attention to the PixelFormat
            Bitmap newImg = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            // write in the byte data
            BitmapData bitmapData = newImg.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            IntPtr srcPtr = bitmapData.Scan0;

            // width of stride, which sometimes can be different from width
            // for instance: 450*300 of the homeworkk
            int stride = bitmapData.Stride;
            int offset = stride - width;

            // two pointers for scan and data
            int posScan = 0, posData = 0;
            byte[] scanData = new byte[bytes];
            for (int i = 0; i < height; ++i)
            {
                for (int j = 0; j < width; ++j)
                {
                    for (int k = 0; k < 3; ++k)
                        scanData[posScan++] = newData[posData++];
                }
                // jump over the offside at the end of each line
                // posScan += offset;
            }

            // neglect the stride, which will cause error
            // Marshal.Copy(newData, 0, srcPtr, height * width);
            // accurate way
            Marshal.Copy(scanData, 0, srcPtr, height * width * 3);
            newImg.UnlockBits(bitmapData);


            // rewrite and show
            img = newImg;
            srcData = newData;
            RData = newRData;
            GData = newGData;
            BData = newBData;
            label1.Text = img.Width + "*" + img.Height;
            pictureBox1.Image = img;
        }

        // calculate the histogram data
        private int[] Cal_Hist(byte[] data) {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            int[] histoData = new int[256];
            for (int i = 0; i < 256; ++i)
                histoData[i] = 0;
            for (int i = 0; i < height; ++i) {
                for (int j = 0; j < width; ++j) {
                    histoData[data[i * width + j]]++;
                } 
            }
            return histoData;
        }

        // calculate the average histogram of RGB
        private int[] Cal_Hist_Ave(int width, int height, byte[] RData, byte[] GData, byte[] BData) {
            int[] histoData = new int[256];
            // calculate RGB histogram indivisually
            int[] RHisto = Cal_Hist(RData);
            // G channel
            int[] GHisto = Cal_Hist(GData);
            // B channel
            int[] BHisto = Cal_Hist(BData);
            for (int i = 0; i < 256; ++i)
            {
                histoData[i] = RHisto[i] + GHisto[i] + BHisto[i];
                histoData[i] /= 3;
            }
            return histoData;
        }

        // histogram equalization
        private int[] Equalize_Hist(int width, int height, int[] histoData) {
            // pixels of the image
            int pixels = width * height;
            // calculate the histo-qualization function
            int[] histoChange = new int[256];
            int sum = 0;
            for (int i = 0; i < 256; ++i) {
                sum += histoData[i];
                histoChange[i] = 255 * sum / pixels;
            }
            return histoChange;
            
        }

        // RGB histogram equalization post-operation
        private void RGB_Histo_Equal(int width, int height, int[] histoChangeR, int[] histoChangeG, int[] histoChangeB, byte[] RData, byte[] GData, byte[] BData) {
            // pixels of the image
            int pixels = width * height;
            // array to hold the new data
            byte[] tempRData = new byte[pixels];
            byte[] tempGData = new byte[pixels];
            byte[] tempBData = new byte[pixels];
            // change the original image;
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    tempRData[i * width + j] = (byte)histoChangeR[RData[i * width + j]];
                    tempGData[i * width + j] = (byte)histoChangeG[GData[i * width + j]];
                    tempBData[i * width + j] = (byte)histoChangeB[BData[i * width + j]];
                }
            BuildBitmap(width, height, tempRData, tempGData, tempBData);
        }

        // HSI histogram equalization post-operation
        private void HSI_Histo_Equal(int width, int height, int[] histoChange, byte[] IData, HSI hsiImage)
        {
            // pixels of the image
            int pixels = width * height;
            // change the original image;
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    hsiImage.IData[i * width + j] = histoChange[IData[i * width + j]];
                }
            HSI_2_RGB(width, height, hsiImage);
        }

        // convert RGB TO HSI
        private HSI RGB_2_HSI(int width, int height, byte[] RData, byte[] GData, byte[] BData) {
            // declare
            HSI hsiImage = new HSI();
            int pixels = width * height;
            hsiImage.HData = new double[pixels];
            hsiImage.SData = new double[pixels];
            hsiImage.IData = new double[pixels];
            // calculate HSI
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int pos = i * width + j;
                    double R = RData[pos];
                    double G = GData[pos];
                    double B = BData[pos];
                    double theta = Math.Acos(0.5*(R + R - G - B)/
                        Math.Sqrt((R - G) * (R - G) + (R - B) * (G - B)));
                    theta *= 180 / Math.PI;
                    if (GData[pos] < BData[pos]) {
                        theta = 360 - theta;
                    }
                    hsiImage.HData[pos] = theta;
                    hsiImage.SData[pos] = 1.0 - 3.0 * Math.Min(R, Math.Min(G, B)) / (R + G + B);
                    hsiImage.IData[pos] = (R + G + B) / 3.0;
                }
            return hsiImage;
        }

        // convert HSI TO RGB
        private RGB HSI_2_RGB(int width, int height, HSI hsiImage)
        {
            // declare
            RGB rgbImage = new RGB();
            int pixels = width * height;
            rgbImage.RData = new byte[pixels];
            rgbImage.GData = new byte[pixels];
            rgbImage.BData = new byte[pixels];
            // calculate RGB
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int pos = i * width + j;
                    int H = (int)hsiImage.HData[pos];
                    double S = hsiImage.SData[pos];
                    double I = hsiImage.IData[pos];
                    if (H >= 0 && H < 120)
                    {
                        rgbImage.BData[pos] = (byte)(I * (1 - S));
                        rgbImage.RData[pos] = (byte)(I * (1 + S * Math.Cos(H * Math.PI / 180.0) / Math.Cos((60 - H) * Math.PI / 180.0)));
                        rgbImage.GData[pos] = (byte)(3 * I - rgbImage.BData[pos] - rgbImage.RData[pos]);
                    }
                    else if (H >= 120 && H < 240)
                    {
                        H -= 120;
                        rgbImage.RData[pos] = (byte)(I * (1 - S));
                        rgbImage.GData[pos] = (byte)(I * (1 + S * Math.Cos(H * Math.PI / 180.0) / Math.Cos((60 - H) * Math.PI / 180.0)));
                        rgbImage.BData[pos] = (byte)(3 * I - rgbImage.GData[pos] - rgbImage.RData[pos]);
                    }
                    else
                    {
                        H -= 240;
                        rgbImage.GData[pos] = (byte)(I * (1 - S));
                        rgbImage.BData[pos] = (byte)(I * (1 + S * Math.Cos(H * Math.PI / 180) / Math.Cos((60 - H) * Math.PI / 180)));
                        rgbImage.RData[pos] = (byte)(3 * I - rgbImage.GData[pos] - rgbImage.BData[pos]);
                    }
                }
            BuildBitmap(img.Width, img.Height, rgbImage.RData, rgbImage.GData, rgbImage.BData);
            return rgbImage;
        }

        // open an image
        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                path = openFileDialog1.FileName;
                LoadBitmap(path);                
            }
        }

        // histogram RGB seperately
        private void button2_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // R channel
            int[] RHisto = Cal_Hist(RData);
            // G channel
            int[] GHisto = Cal_Hist(GData);
            // B channel
            int[] BHisto = Cal_Hist(BData);

            // calculate
            int[] histoChangeR = Equalize_Hist(img.Width, img.Height, RHisto);
            int[] histoChangeG = Equalize_Hist(img.Width, img.Height, GHisto);
            int[] histoChangeB = Equalize_Hist(img.Width, img.Height, BHisto);
            RGB_Histo_Equal(img.Width, img.Height, histoChangeR, histoChangeG, histoChangeB, RData, GData, BData);
        }

        // reload the image
        private void button4_Click(object sender, EventArgs e)
        {
            if (path == "") return;
            LoadBitmap(path);
        }

        // save to file
        private void button3_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
                img.Save(saveFileDialog1.FileName, ImageFormat.Bmp);
            }
        }

        // show RGB histogram
        private void button6_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // R channel
            int[] RHisto = Cal_Hist(RData);
            Form2 Rform2 = new Form2(RHisto, "R");
            Rform2.Show();
            // G channel
            int[] GHisto = Cal_Hist(GData);
            Form2 Gform2 = new Form2(GHisto, "G");
            Gform2.Show();
            // B channel
            int[] BHisto = Cal_Hist(BData);
            Form2 Bform2 = new Form2(BHisto, "B");
            Bform2.Show();
        }

        // average filter
        private void button5_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            BuildBitmap(img.Width, img.Height, RData, GData, BData);
            // filter it
            // Filter2d(srcData, level, filter);
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;

        }

        private void saveFileDialog1_FileOk(object sender, CancelEventArgs e)
        {

        }

        // RGB histogram equalization averagely
        private void button7_Click_1(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int[] averageHisto = Cal_Hist_Ave(img.Width, img.Height, RData, GData, BData);
            int[] histoChange = Equalize_Hist(img.Width, img.Height, averageHisto);
            RGB_Histo_Equal(img.Width, img.Height, histoChange, histoChange, histoChange, RData, GData, BData);
        }

        // show HSI image
        private void button9_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            HSI hsiImage = RGB_2_HSI(img.Width, img.Height, RData, GData, BData);
            RGB rgbImage = HSI_2_RGB(img.Width, img.Height, hsiImage);
        }

        // show HSI histogram on I channel
        private void button12_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // calculate hsi image;
            HSI hsiImage = RGB_2_HSI(img.Width, img.Height, RData, GData, BData);
            // convert i channel to byte
            byte[] IData = new byte[img.Width * img.Height];
            for (int i = 0; i < img.Width * img.Height; ++i) {
                IData[i] = (byte)hsiImage.IData[i];
            }
            // I channel histogram
            int[] IHisto = Cal_Hist(IData);
            Form2 Iform2 = new Form2(IHisto, "I");
            Iform2.Show();
        }

        // histogram equalization on I channel
        private void button10_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // calculate hsi image;
            HSI hsiImage = RGB_2_HSI(img.Width, img.Height, RData, GData, BData);
            // convert i channel to byte
            byte[] IData = new byte[img.Width * img.Height];
            for (int i = 0; i < img.Width * img.Height; ++i)
            {
                IData[i] = (byte)hsiImage.IData[i];
            }
            // I channel histogram
            int[] IHisto = Cal_Hist(IData);
            int[] histoChange = Equalize_Hist(img.Width, img.Height, IHisto);
            HSI_Histo_Equal(img.Width, img.Height, histoChange, IData, hsiImage);
        }
    }

    public class HSI
    {
        public double[] HData;
        public double[] SData;
        public double[] IData;
    }

    public class RGB {
        public byte[] RData;
        public byte[] GData;
        public byte[] BData;
    }
}
