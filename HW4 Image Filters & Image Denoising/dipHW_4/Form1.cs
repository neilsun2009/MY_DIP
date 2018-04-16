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

namespace dipHW_4
{
    public partial class Form1 : Form
    {
        string path;
        private Bitmap img;
        byte[] srcData;
        int[] histoData;
        Random random = new Random();

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

        // arithmetic mean filter
        private void ArithMeanFilter(byte[] srcData, int level) {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize the offset from current pixel
            int[] offset = new int[level * level];
            int temNum = 0;
            for (int i = - level / 2; i <= level/2; ++i) {
                for (int j = -level / 2; j <= level / 2; ++j) {
                    offset[temNum++] = i * width + j;
                }
            }
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j) {
                    double temp = 0;
                    for (int k = 0; k < level * level; ++k) {
                        // zero padding
                        int pos = i * width + j + offset[k];
                        double data;
                        if (pos >= 0 && pos < height * width)
                        {
                            data = srcData[pos];
                        }
                        else {
                            data = 0;
                        }
                        // applying filter
                        temp += data / (level*level);
                    }
                    // formatting into byte
                    if (temp < 0) temp = 0;
                    if (temp > 255) temp = 255;
                    tempData[i * width + j] = (byte)temp;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // geometric mean filter
        private void GeoMeanFilter(byte[] srcData, int level)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize the offset from current pixel
            int[] offset = new int[level * level];
            int temNum = 0;
            for (int i = -level / 2; i <= level / 2; ++i)
            {
                for (int j = -level / 2; j <= level / 2; ++j)
                {
                    offset[temNum++] = i * width + j;
                }
            }
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    double temp = 1;
                    for (int k = 0; k < level * level; ++k)
                    {
                        // zero padding
                        int pos = i * width + j + offset[k];
                        double data;
                        if (pos >= 0 && pos < height * width)
                        {
                            data = srcData[pos];
                        }
                        else {
                            data = 0;
                        }
                        // applying filter
                        temp *= Math.Pow(data, 1.0 / (level * level));
                    }
                    // formatting into byte
                    if (temp < 0) temp = 0;
                    if (temp > 255) temp = 255;
                    tempData[i * width + j] = (byte)temp;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // harmonic mean filter
        private void HarmoMeanFilter(byte[] srcData, int level)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize the offset from current pixel
            int[] offset = new int[level * level];
            int temNum = 0;
            for (int i = -level / 2; i <= level / 2; ++i)
            {
                for (int j = -level / 2; j <= level / 2; ++j)
                {
                    offset[temNum++] = i * width + j;
                }
            }
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    double temp = 0;
                    for (int k = 0; k < level * level; ++k)
                    {
                        // zero padding
                        int pos = i * width + j + offset[k];
                        double data;
                        if (pos >= 0 && pos < height * width)
                        {
                            data = srcData[pos];
                        }
                        else {
                            data = 0;
                        }
                        // applying filter
                        temp += 1/(data + 0.00000001);
                    }
                    temp = level * level / temp;
                    // formatting into byte
                    if (temp < 0) temp = 0;
                    if (temp > 255) temp = 255;
                    tempData[i * width + j] = (byte)temp;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // counterharmonic mean filter
        private void CounterHarmoMeanFilter(byte[] srcData, int level, double q)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize the offset from current pixel
            int[] offset = new int[level * level];
            int temNum = 0;
            for (int i = -level / 2; i <= level / 2; ++i)
            {
                for (int j = -level / 2; j <= level / 2; ++j)
                {
                    offset[temNum++] = i * width + j;
                }
            }
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    double temp1 = 0;
                    double temp2 = 0;
                    for (int k = 0; k < level * level; ++k)
                    {
                        // zero padding
                        int pos = i * width + j + offset[k];
                        double data;
                        if (pos >= 0 && pos < height * width)
                        {
                            data = srcData[pos];
                        }
                        else {
                            data = 0;
                        }
                        // applying filter
                        temp1 += Math.Pow(data, q + 1.0);
                        temp2 += Math.Pow(data, q);
                    }
                    temp1 /= temp2;
                    // formatting into byte
                    if (temp1 < 0) temp1 = 0;
                    if (temp1 > 255) temp1 = 255;
                    tempData[i * width + j] = (byte)temp1;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // min filter
        private void MinFilter(byte[] srcData, int level)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize the offset from current pixel
            int[] offset = new int[level * level];
            int temNum = 0;
            for (int i = -level / 2; i <= level / 2; ++i)
            {
                for (int j = -level / 2; j <= level / 2; ++j)
                {
                    offset[temNum++] = i * width + j;
                }
            }
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int temp = 255;
                    for (int k = 0; k < level * level; ++k)
                    {
                        // zero padding
                        int pos = i * width + j + offset[k];
                        int data;
                        if (pos >= 0 && pos < height * width)
                        {
                            data = srcData[pos];
                        }
                        else {
                            data = 0;
                        }
                        // applying filter
                        temp = Math.Min(temp, data);
                    }
                    // formatting into byte
                    if (temp < 0) temp = 0;
                    if (temp > 255) temp = 255;
                    tempData[i * width + j] = (byte)temp;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // median filter
        private void MedianFilter(byte[] srcData, int level)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize the offset from current pixel
            int[] offset = new int[level * level];
            int temNum = 0;
            for (int i = -level / 2; i <= level / 2; ++i)
            {
                for (int j = -level / 2; j <= level / 2; ++j)
                {
                    offset[temNum++] = i * width + j;
                }
            }
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int[] temp = new int[level * level];
                    for (int k = 0; k < level * level; ++k)
                    {
                        // zero padding
                        int pos = i * width + j + offset[k];
                        int data;
                        if (pos >= 0 && pos < height * width)
                        {
                            data = srcData[pos];
                        }
                        else {
                            data = 0;
                        }
                        // applying filter
                        temp[k] = data;
                    }
                    int temp1;
                    for (int i1 = 0; i1 < level * level; ++i1)
                        for (int j1 = i1 + 1; j1 < level * level; ++j1) {
                            if (temp[i1] > temp[j1]) {
                                temp1 = temp[i1];
                                temp[i1] = temp[j1];
                                temp[j1] = temp1;
                            }
                        }
                    temp1 = temp[level * level / 2];
                    // formatting into byte
                    if (temp1 < 0) temp1 = 0;
                    if (temp1 > 255) temp1 = 255;
                    tempData[i * width + j] = (byte)temp1;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // gaussian calculation
        private double GaussianCal(double mean, double variance) {
            
            double r1 = random.NextDouble();
            double r2 = random.NextDouble();
            double gaussian = Math.Sqrt(-2 * Math.Log(r2)) * Math.Sin(2*Math.PI*r1);
            // Console.WriteLine("gaussian: " + gaussian);
            return mean + variance * gaussian;
        }

        // gaussian noise
        private void GaussianNoise(byte[] srcData, double mean, double variance)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int pos = i * width + j;
                    double temp = srcData[pos];
                    // a random value
                    // double value = random.NextDouble() - 0.5 + mean;
                    // double z = value * variance * 2;
                    double gaussian = GaussianCal(mean, variance);
                    temp += gaussian;
                    // double nowPosibility = random.NextDouble();
                    // Console.WriteLine(posibility + " : " + nowPosibility);
                    // if (nowPosibility < posibility) {
                        // value = (value - mean + 0.5) / (1);
                    //     temp +=  value * 128;
                    // }
                    // formatting into byte
                    if (temp < 0) temp = 0;
                    if (temp > 255) temp = 255;
                    tempData[pos] = (byte)temp;
                }
            // write the new image
            BuildBitmap(width, height, tempData);
        }

        // salt and pepper noise
        private void SaltPepperNoise(byte[] srcData, double salt, double pepper)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // array to hold the new data
            byte[] tempData = new byte[width * height];
            // generize new imgae data
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int pos = i * width + j;
                    double temp = srcData[pos];
                    // use probability to calculate
                    double probSalt = random.NextDouble();
                    double probPepper = random.NextDouble();
                    if (probPepper < pepper && probSalt < salt)
                    {
                        double choose = random.NextDouble();
                        if (choose < 0.5)
                        {
                            temp = 0;
                        }
                        else {
                            temp = 255;
                        }
                    }
                    else if (probSalt < salt)
                    {
                        temp = 255;
                    }
                    else if (probPepper < pepper) {
                        temp = 0;
                    }
                    // formatting
                    if (temp < 0) temp = 0;
                    if (temp > 255) temp = 255;
                    tempData[pos] = (byte)temp;
                }
            // write the new image
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

        // counterharmonic mean filter
        private void button2_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            double q = Double.Parse(textBox6.Text);
            // filter it
            CounterHarmoMeanFilter(srcData, level, q);
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

        // harmonic mean filter
        private void button6_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            // filter it
            HarmoMeanFilter(srcData, level);

        }

        // arithmetic mean filter
        private void button5_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            // filter it
            ArithMeanFilter(srcData, level);
        }

        // geometric mean filter
        private void button7_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            // filter it
            GeoMeanFilter(srcData, level);

        }

        // min filter
        private void button9_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            // filter it
            MinFilter(srcData, level);
        }

        // median filter
        private void button8_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            // filter it
            MedianFilter(srcData, level);
        }

        private void label2_Click(object sender, EventArgs e)
        {

        }

        // gaussian noise
        private void button10_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            double mean = Double.Parse(textBox1.Text);
            double variance = Double.Parse(textBox2.Text);
            GaussianNoise(srcData, mean, variance);
        }

        // salt and pepper noise
        private void button11_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            double salt = Double.Parse(textBox5.Text);
            double pepper = Double.Parse(textBox4.Text);
            SaltPepperNoise(srcData, salt, pepper);
        }
    }
}
