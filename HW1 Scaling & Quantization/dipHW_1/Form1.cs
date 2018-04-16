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

namespace dipHW_1
{
    public partial class Form1 : Form
    {
        string path;
        private Bitmap img;
        byte[] srcData;

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
                ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
            srcData = new byte[img.Width * img.Height];
            IntPtr srcPtr = bitmapData.Scan0;
            Marshal.Copy(srcPtr, srcData, 0, img.Width * img.Height);
            // pay attention: order in byte array: height first
            img.UnlockBits(bitmapData);
        }

        // build a new bitmap with byte data
        private void BuildBitmap(int width, int height, byte[] newData) {
            // pay attention to the PixelFormat
            Bitmap newImg = new Bitmap(width, height, PixelFormat.Format8bppIndexed);
            // write in the byte data
            BitmapData bitmapData = newImg.LockBits(new Rectangle(0, 0, width, height),
                ImageLockMode.ReadWrite, PixelFormat.Format8bppIndexed);
            IntPtr srcPtr = bitmapData.Scan0;

            // width of stride, which sometimes can be different from width
            // for instance: 450*300 of the homeworkk
            int stride = bitmapData.Stride;
            int offset = stride - width;

            // two pointers for scan and data
            int posScan = 0, posData = 0;
            byte[] scanData = new byte[stride * height];
            for (int i = 0; i < height; ++i)
            {
                for (int j = 0; j < width; ++j)
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


            // another method:
            // user setPixel to set color to every pixel
            // for (int i = 0; i < width; ++i)
            //     for (int j = 0; j < height; ++j)
            //         newImg.SetPixel(i, j, Color.FromArgb(newData[j * width + i], newData[j * width + i], newData[j * width + i]));
            
            // override the color palette to be grayscale
            ColorPalette tempPalette;
            using (Bitmap tempBmp = new Bitmap(1, 1, PixelFormat.Format8bppIndexed))
            {
                tempPalette = tempBmp.Palette;
            }
            for (int i = 0; i < 256; i++)
            {
                tempPalette.Entries[i] = Color.FromArgb(i, i, i);
            }

            newImg.Palette = tempPalette;

            // rewrite and show
            img = newImg;
            srcData = newData;
            label1.Text = img.Width + "*" + img.Height;
            pictureBox1.Image = img;
        }

        // main function of scaling
        private void Scale(byte[] srcData, int width, int height) {
            // width and height of the original image
            int oriWidth = img.Width;
            int oriHeight = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[height * width];
            // scale ratio of width and height
            double widthScale = (double)oriWidth / (double)width;
            double heightScale = (double)oriHeight / (double)height;
            // calculate new data
            for (int i = 0; i < height; ++i) {
                // find the closest y-index value
                double dbY = (double)i * heightScale;
                int intY = (int)dbY;
                double restY = dbY - (double)intY;
                // the next y value should not be bigger than oriHeight
                int intY2 = intY + 1;
                if (intY2 >= oriHeight)
                {
                    intY2 -= 1;
                }
                for (int j = 0; j < width; ++j) {
                    // closest x value
                    double dbX = (double)j * widthScale;
                    int intX = (int)dbX;
                    double restX = dbX - (double)intX;
                    int intX2 = intX + 1;
                    // next x value should not be bigger than oriWidth
                    if (intX2 >= oriWidth)
                    {
                        intX2 -= 1;
                    }
                    // bilinear interpolation
                    tempData[i * width + j] = (byte)((double)srcData[intY * oriWidth + intX] * (1.0 - restX) * (1.0 - restY) +
                        (double)srcData[intY * oriWidth + intX2] * (restX) * (1.0- restY) +
                        (double)srcData[intY2 * oriWidth + intX] * (1.0- restX) * (restY) +
                        (double)srcData[intY2 * oriWidth + intX2] * (restX) * (restY));
                }
            }
            BuildBitmap(width, height, tempData);
        }

        // quantilize image by plane number
        private void Quantilize(byte[] srcData, int level) {
            int plane = (int)Math.Round(Math.Log(level) / Math.Log(2));
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // count represents the number of pixels
            int count = width * height;
            byte[] tempData = new byte[count];
            // how many times the selected planes should be repeated
            int times = (int)Math.Ceiling(8.0 / (double)plane);
            // Console.WriteLine(plane + ":" + times);

            // calculate new data
            for (int i = 0; i < count; ++i) {
                // select the last n planes
                int tem = srcData[i] & ((1 << plane) - 1);
                // repeat the bits
                for (int j = 1; j < times; ++j) {
                    tem = (tem << plane) | tem;
                }
                tem &= 255;
                tempData[i] = (byte)tem;
            }
            BuildBitmap(width, height, tempData);
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

        // scale an image
        private void button2_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int width = Int32.Parse(textBox1.Text);
            int height = Int32.Parse(textBox2.Text);
            Scale(srcData, width, height);
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

        // quantilization
        private void button5_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            Quantilize(srcData, level);

        }
    }
}
