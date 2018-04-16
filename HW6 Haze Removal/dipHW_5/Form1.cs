using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace dipHW_5
{

    public partial class Form1 : Form
    {

        public class RGB
        {
            public byte[] RData;
            public byte[] GData;
            public byte[] BData;
        }

        private Bitmap srcImg;
        private Bitmap resultImg;
        byte[] srcData;
        byte[] resultData;
        RGB srcRGB;
        RGB resultRGB;



        public Form1()
        {
            InitializeComponent();
        }

        // load and initialize from file
        private void LoadBitmap(string path)
        {
            // read from file
            Bitmap img;
            srcImg = (Bitmap)Image.FromFile(path);
            pictureBox1.Image = srcImg;
            img = srcImg;
            int height = img.Height;
            int width = img.Width;

            // read byte data
            BitmapData bitmapData = img.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite, PixelFormat.Format24bppRgb);
            int bytes = width * height * 3;
            int stride = bitmapData.Stride;
            int offset = stride - width * 3;
            srcData = new byte[bytes];
            byte[] scanData = new byte[stride * height];
            srcRGB = new RGB();
            srcRGB.RData = new byte[bytes / 3];
            srcRGB.GData = new byte[bytes / 3];
            srcRGB.BData = new byte[bytes / 3];
            IntPtr srcPtr = bitmapData.Scan0;
            // Marshal.Copy(srcPtr, srcData, 0, bytes);
            Marshal.Copy(srcPtr, scanData, 0, stride * height);
            /*for (int i = 0, j = 0; i < stride * img.Height; i += 3, j++)
            {
                srcRGB.RData[j] = scanData[i];
                srcRGB.GData[j] = scanData[i + 1];
                srcRGB.BData[j] = scanData[i + 2];
            }*/
            int posScan = 0, posData = 0;
            for (int i = 0; i < height; ++i)
            {
                for (int j = 0; j < width; ++j)
                {
                    srcRGB.RData[posData] = scanData[posScan++];
                    srcRGB.GData[posData] = scanData[posScan++];
                    srcRGB.BData[posData++] = scanData[posScan++];
                }
                // jump over the offside at the end of each line
                posScan += offset;
            }


            // pay attention: order in byte array: height first
            img.UnlockBits(bitmapData);
        }

        // build a new bitmap with byte data
        private void BuildBitmap(int width, int height, RGB newRGB)
        {
            // generate new srcData
            int bytes = width * height * 3;
            byte[] newData = new byte[bytes];
            for (int i = 0, j = 0; i < bytes; i += 3, j++)
            {
                newData[i] = newRGB.RData[j];
                newData[i + 1] = newRGB.GData[j];
                newData[i + 2] = newRGB.BData[j];
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
            int offset = stride - width*3;

            // two pointers for scan and data
            int posScan = 0, posData = 0;
            byte[] scanData = new byte[height*stride];
            for (int i = 0; i < height; ++i)
            {
                for (int j = 0; j < width *3; ++j)
                {
                    scanData[posScan++] = newData[posData++];
                }
                // jump over the offside at the end of each line
                posScan += offset;
            }

            // neglect the stride, which will cause error
            // Marshal.Copy(newData, 0, srcPtr, height * width);
            // accurate way
            Marshal.Copy(scanData, 0, srcPtr, height * stride);
            newImg.UnlockBits(bitmapData);


            // rewrite and show
            resultImg = newImg;
            resultData = newData;
            pictureBox3.Image = resultImg;
        }

        // calculate data of a specific channel using guided filter
        private byte[] GuidedFilter(byte[] data, int width, int height, int r, double epsilon) {
            // calculate intermediate arguments
            double[,] mean = new double[height + 2 * r - 2, width + 2 * r - 2];
            double[,] square = new double[height + 2 * r - 2, width + 2 * r - 2];
            double[,] variance = new double[height + 2 * r - 2, width + 2 * r - 2];
            double[,] a = new double[height + 2 * r - 2, width + 2 * r - 2];
            double[,] b = new double[height + 2 * r - 2, width + 2 * r - 2];
            // first round of loop, determine (Pk)^2 and ave(Pk)
            for (int i = 0; i < height + 2 * r - 2; ++i)
                for (int j = 0; j < width + 2 * r - 2; ++j) {
                    mean[i, j] = 0;
                    square[i, j] = 0;
                    for (int ii = -r+1; ii <= r-1; ++ii)
                        for (int jj = -r+1; jj <= r-1; ++jj) {
                            int nowI = i + ii, nowJ = j + jj;
                            int value = 0;
                            if (nowI >= 0 && nowI < height && nowJ >= 0 && nowJ < width) {
                                value = data[nowI * width + nowJ];
                            }
                            mean[i, j] += value;
                            square[i, j] += value * value;
                        }
                    mean[i, j] /= (2*r-1)*(2*r-1);
                }
            // second round of loop, determine variance(Pk), ak and bk
            for (int i = 0; i < height + 2 * r - 2; ++i)
                for (int j = 0; j < width + 2 * r - 2; ++j)
                {
                    variance[i, j] = 0;
                    for (int ii = -r + 1; ii <= r - 1; ++ii)
                        for (int jj = -r + 1; jj <= r - 1; ++jj)
                        {
                            int nowI = i + ii, nowJ = j + jj;
                            int value = 0;
                            if (nowI >= 0 && nowI < height && nowJ >= 0 && nowJ < width)
                            {
                                value = data[nowI * width + nowJ];
                            }
                            variance[i, j] += (value - mean[i, j]) * (value - mean[i, j]);
                        }
                    variance[i, j] /= (2 * r - 1) * (2 * r - 1);
                    a[i, j] = (square[i, j] / ((2 * r - 1) * (2 * r - 1)) - mean[i, j] * mean[i, j]) / (variance[i, j] + epsilon);
                    // a[i, j] = 0.2;
                    b[i, j] = mean[i, j] * (1 - a[i, j]);
                }
            // determine the return value
            byte[] re = new byte[width * height];
            for (int i = 0; i < height; ++i) {
                for (int j = 0; j < width; ++j) {
                    double aa = 0, bb = 0;
                    int pixel = i * width + j;
                    for (int ii = 0; ii < 2*r-1; ++ii)
                        for (int jj = 0; jj < 2*r-1; ++jj) {
                            aa += a[i + ii, j + jj];
                            bb += b[i + ii, j + jj];
                        }
                    aa /= (2 * r - 1) * (2 * r - 1);
                    bb /= (2 * r - 1) * (2 * r - 1);
                    double value = aa * data[pixel] + bb;
                    /*if (value > 255)
                    {
                        value = 255;
                    }
                    else if (value < 0)
                    {
                        value = 0;
                    }*/
                    re[pixel] = (byte)value;
                }
            }
            return re;
        }
        

        // open source image
        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                String path = openFileDialog1.FileName;
                LoadBitmap(path);
            }
        }



        // save to file
        private void button4_Click(object sender, EventArgs e)
        {
            if (pictureBox3.Image == null) return;
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                resultImg.Save(saveFileDialog1.FileName, ImageFormat.Bmp);
            }
        }

        // calculate the result image
        private void button3_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // get the parameters
            int r = Int32.Parse(textBox1.Text);
            double epsilon = Double.Parse(textBox2.Text);
            epsilon *= 255;
            epsilon *= epsilon;
            // guided filter for RGB
            resultRGB = new RGB();
            resultRGB.RData = GuidedFilter(srcRGB.RData, srcImg.Width, srcImg.Height, r, epsilon);
            resultRGB.GData = GuidedFilter(srcRGB.GData, srcImg.Width, srcImg.Height, r, epsilon);
            resultRGB.BData = GuidedFilter(srcRGB.BData, srcImg.Width, srcImg.Height, r, epsilon);
            // resultRGB.RData = srcRGB.RData;
            // resultRGB.GData = srcRGB.GData;
            // resultRGB.BData = srcRGB.BData;
            BuildBitmap(srcImg.Width, srcImg.Height, resultRGB);
        }
    }
}
