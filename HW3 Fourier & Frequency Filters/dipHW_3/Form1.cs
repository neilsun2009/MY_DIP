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

namespace dipHW_3
{
    public partial class Form1 : Form
    {
        string path;
        private Bitmap img;
        byte[] srcData;
        
        int[] histoData;

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
            Console.WriteLine("printing over");
        }
        
        // calculate the histogram data
        private void Cal_Hist(byte[] srcData, int width, int height)
        {
            histoData = new int[256];
            for (int i = 0; i < 256; ++i)
                histoData[i] = 0;
            for (int i = 0; i < height; ++i)
            {
                for (int j = 0; j < width; ++j)
                {
                    histoData[srcData[i * width + j]]++;
                }
            }
        }

        // histogram equalization
        private void Equalize_Hist(byte[] srcData, int width, int height)
        {
            int pixels = width * height;
            // array to hold the new data
            byte[] tempData = new byte[pixels];
            // calculate histogram data
            Cal_Hist(srcData, width, height);
            // calculate the histo-qualization function
            int[] histoChange = new int[256];
            int sum = 0;
            /*for (int i = 0; i < 128; ++i)
            {
                sum += histoData[i];
                histoChange[i] = 127 * sum / pixels;
            }
            for (int i = 128; i < 256; ++i)
            {
                sum += histoData[i];
                histoChange[i] = 128 + 127 * sum / pixels;
            }*/
            for (int i = 0; i < 256; ++i)
            {
                sum += histoData[i];
                histoChange[i] = 256 * sum / pixels;
            }
            // change the original image;
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    tempData[i * width + j] = (byte)histoChange[srcData[i * width + j]];
                }
            BuildBitmap(width, height, tempData);
        }

        // core algorithm
        private double[,] coreFourier(double[,] data, int height, int width, bool flag, int level) {
            // flag 0 : dft
            // flag 1 : idft
            int er = flag ? 2 : -2;
            double[,] re = new double[width * height, 2];
            for (int i = 0; i < height; ++i)
            {
                if (i % 100 == 0) Console.WriteLine(i);
                for (int j = 0; j < width; ++j)
                {
                    int point = i * width + j;
                    re[point, 1] = 0;
                    re[point, 0] = 0;
                    int limit = (level == 1) ? width : height;
                    for (int k = 0; k < limit; ++k)
                    {
                        double exp = 0;
                        int tempPoint = 0;
                        if (level == 1)
                        {
                            exp = (double)(k * j) / (double)width;
                            tempPoint = i * width + k;
                        }
                        else {
                            exp = (double)(k * i) / (double)height;
                            tempPoint = k * width + j;
                        }
                        re[point, 0] += data[tempPoint, 0] * Math.Cos(er * Math.PI * exp) - data[tempPoint, 1] * Math.Sin(er * Math.PI * exp);
                        re[point, 1] += data[tempPoint, 1] * Math.Cos(er * Math.PI * exp) + data[tempPoint, 0] * Math.Sin(er * Math.PI * exp);
                    }
                }
            }
            return re;
        }

        // recursive calculation of fft
        private double[,] calFFT(double[,] data, bool flag, int k) {
            // flag 0 : dft
            // flag 1 : idft
            int er = flag ? 2 : -2;
            double[,] re = new double[k,2];
            if (k == 1)
            {
                re[0, 0] = data[0, 0];
                re[0, 1] = data[0, 1];
            }
            else {
                // recusive
                double[,] even = new double[k / 2, 2];
                double[,] odd = new double[k / 2 , 2];
                double[,] evenData = new double[k / 2, 2];
                double[,] oddData = new double[k / 2, 2];
                for (int i = 0; i < k / 2; ++i) {
                    evenData[i, 0] = data[i * 2, 0];
                    evenData[i, 1] = data[i * 2, 1];
                    oddData[i, 0] = data[i * 2 + 1, 0];
                    oddData[i, 1] = data[i * 2 + 1, 1];
                }
                even = calFFT(evenData, flag, k / 2);
                odd = calFFT(oddData, flag, k / 2);
                // packaging
                for (int i = 0; i < k / 2; ++i) {
                    double exp = Math.PI * (double)(er*i) / k;
                    re[i, 0] = even[i, 0] + odd[i, 0] * Math.Cos(exp) - odd[i, 1] * Math.Sin(exp);
                    re[i, 1] = even[i, 1] + odd[i, 1] * Math.Cos(exp) + odd[i, 0] * Math.Sin(exp);
                    re[i + k/2, 0] = even[i, 0] - odd[i, 0] * Math.Cos(exp) + odd[i, 1] * Math.Sin(exp);
                    re[i + k/2, 1] = even[i, 1] - odd[i, 1] * Math.Cos(exp) - odd[i, 0] * Math.Sin(exp);
                }
                
            }
            return re;
        }

        // core fourier for fft
        private double[,] coreFastFourier(double[,] data, int height, int width, bool flag, int level)
        {
            // flag 0 : dft
            // flag 1 : idft
            int er = flag ? 2 : -2;
            double[,] re = new double[width * height, 2];
            int limit = (level == 1) ? width : height;
            int outerLimit = (level == 1) ? height : width;
            for (int i = 0; i < outerLimit; ++i) {
               // Console.WriteLine("i=" + i);
                double[,] inputData = new double[limit, 2];
                for (int j = 0; j < limit; ++j) {
                    // if (i > 190) Console.WriteLine("j="+ j);
                    int tempPoint = 0;
                    if (level == 1)
                    {
                        tempPoint = i * width + j;
                    }
                    else {
                        tempPoint = j * width + i;
                    }
                    inputData[j, 0] = data[tempPoint, 0];
                    inputData[j, 1] = data[tempPoint, 1];
                }
                double[, ] value = calFFT(inputData, flag, limit);
                for (int j = 0; j < limit; ++j) {
                    int tempPoint = 0;
                    if (level == 1)
                    {
                        tempPoint = i * width + j;
                    }
                    else {
                        tempPoint = j * width + i;
                    }
                    re[tempPoint, 0] = value[j, 0];
                    re[tempPoint, 1] = value[j, 1];
                }
            }
            return re;
        }

        // calculate the ceiling integer which is a time of 2
        private int cal2times(int x) {
            int re = 1;
            while (re < x) {
                re *= 2;
            }
            return re;
        }

        // fft 
        // flag = 0 -> fft, flag = 1 -> ifft
        private double[,] fft2d(double[,] srcData, bool flag, int width, int height)
        {
            // array to hold the new data
            double[,] fourierData;
            double[,] fourierData_1d;
            double[,] tempData = srcData;
            
            if (!flag)
            {
                fourierData_1d = coreFastFourier(tempData, height, width, false, 1);
                fourierData = coreFastFourier(fourierData_1d, height, width, false, 2);

                return fourierData;
            }
            else {
                fourierData_1d = coreFastFourier(tempData, height, width, true, 1);
                tempData = coreFastFourier(fourierData_1d, height, width, true, 2);
                for (int i = 0; i < width * height; ++i)
                {
                    tempData[i, 0] /= width * height;
                    tempData[i, 1] /= width * height;
                }
                // tempData = translate(tempData, width, height);

                postProcess(tempData, width, height);
                return tempData;
            }

        }

        // show the picture of fourier
        private void showFourier(double[,] fourierData, int width, int height) {
            // generate visible image
            double[] rawData = new double[width * height];
            double maxRaw = 0, minRaw = 0;
            byte[] newData = new byte[width * height];
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int point = i * width + j;
                    rawData[point] = (Math.Sqrt(fourierData[point, 0] * fourierData[point, 0]
                        + fourierData[point, 1] * fourierData[point, 1]));
                    // Console.WriteLine(rawData[point]);
                    // log transformation
                    rawData[point] = Math.Log(1 + rawData[point]);
                    maxRaw = Math.Max(maxRaw, rawData[point]);
                    if (point == 0)
                    {
                        minRaw = rawData[point];
                    }
                    else
                    {
                        minRaw = Math.Min(minRaw, rawData[point]);
                    }
                }
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int point = i * width + j;
                    
                    newData[point] = (byte)(255.0 * (rawData[point] - minRaw) / (maxRaw - minRaw));
                }
            
            // Equalize_Hist(newData, width, height);
            Console.WriteLine("printing");
            BuildBitmap(width, height, newData);
        }

        // postprecessing
        // clip the top-left corner of the image and show
        private void postProcess(double[,] data, int width, int height) {
            // generate visible image
            double[] rawData = new double[width * height];
            double maxRaw = 0, minRaw = 0;
            byte[] newData = new byte[width * height];
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int point = i * width + j;
                    rawData[point] = data[point, 0];
                    // Console.WriteLine(rawData[point]);
                    /*if ((i + j) % 2 == 1)
                    {
                        if (rawData[point] >= 128)
                        {
                            rawData[point] -= 128;
                        }
                        else {
                            rawData[point] += 128;
                        }
                    }*/
                    maxRaw = Math.Max(maxRaw, rawData[point]);
                    if (point == 0)
                    {
                        minRaw = rawData[point];
                    }
                    else
                    {
                        minRaw = Math.Min(minRaw, rawData[point]);
                    }
                }
            
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int point = i * width + j;
                    newData[point] = (byte)(255.0 * (rawData[point] - minRaw) / (maxRaw - minRaw));
                    // newData[newPoint] = (byte)rawData[point];
                }
            // translate
            /*for (int i = 0; i < height / 2; ++i)
                for (int j = 0; j < width / 2; ++j)
                {
                    int point = i * width / 2+ j;
                    if ((i + j) % 2 == 1)
                    {
                        int now = (int)newData[point];
                        if (now >= 128)
                        {
                            newData[point] -= 128;
                        }
                        else {
                            newData[point] += 128;
                        }
                    }
                }*/
            // newData = padding(newData, width, height, width / 2, height / 2);
            BuildBitmap(width, height, newData);
        }
        
        // dft 
        // flag = 0 -> dft, flag = 1 -> idft
        private double[,] dft2d(double[,] srcData, bool flag, int width, int height) {
            // array to hold the new data
            double[,] fourierData;
            double[,] fourierData_1d;
            double[,] tempData = srcData;
            if (!flag)
            {
                // dft
                // translate
                /*for (int i = 0; i < height; ++i)
                {
                    for (int j = 0; j < width; ++j)
                    {
                        if ((i + j) % 2 == 1)
                        {
                            int now = (int)tempData[i * width + j, 0];
                            if (now >= 128)
                            {
                                tempData[i * width + j, 0] -= 128;
                            }
                            else {
                                tempData[i * width + j, 0] += 128;
                            }
                        }
                    }
                }*/
                // dft
                /*worst solution
                fourierData = new double[width * height,2];
                fourierData_1d = new double[width * height, 2];
                for (int u = 0; u < height; ++u) {
                    if (u % 50 == 0 || u < 10)
                        Console.WriteLine(u);
                    for (int v = 0; v < width; ++v) {
                        int point = u * width + v;
                        fourierData[point, 0] = 0;
                        fourierData[point, 1] = 0;
                        for (int i = 0; i < height; ++i) {
                            for (int j = 0; j < width; ++j) {
                                double exp = (double)(u * i) / (double)height + (double)(v * j) / (double)width;
                                fourierData[point, 0] += tempData[i * width + j] * Math.Cos(-2 * Math.PI * exp);
                                fourierData[point, 1] += tempData[i * width + j] * Math.Sin(-2 * Math.PI * exp);
                            }
                        }
                    }
                    
                }*/
                // a better way, however still not fast enough
                fourierData_1d = coreFourier(tempData, height, width, false, 1);
                fourierData = coreFourier(fourierData_1d, height, width, false, 2);
                
                return fourierData;
            }
            else {
                fourierData_1d = coreFourier(tempData, height, width, true, 1);
                tempData = coreFourier(fourierData_1d, height, width, true, 2);
                for (int i = 0; i < width * height; ++i)
                {
                    tempData[i, 0] /= width * height;
                    tempData[i, 1] /= width * height;
                }
                // tempData = translate(tempData, width, height);

                // postProcess(tempData, width, height);
                return tempData;
            }
            
        }

        private double[,] padding(double[] data, int width, int height, int toWidth, int toHeight) {
            double[,] re = new double[toWidth * toHeight,2];
            for (int i = 0; i < toHeight; ++i)
                for (int j = 0; j < toWidth; ++j) {
                    int point = i * toWidth + j;
                    re[point, 1] = 0;
                    if (i >= 0 && i < height && j >= 0 && j < width)
                    {
                        re[point, 0] = data[i * width + j];
                    }
                    else {
                        re[point, 0] = 0;
                    }
                }
            return re;
        }
        // an override padding method
        private double[,] padding(double[,] data, int width, int height, int toWidth, int toHeight)
        {
            double[,] re = new double[toWidth * toHeight, 2];
            for (int i = 0; i < toHeight; ++i)
                for (int j = 0; j < toWidth; ++j)
                {
                    int point = i * toWidth + j;
                    
                    if (i >= 0 && i < height && j >= 0 && j < width)
                    {
                        re[point, 0] = data[i * width + j, 0];
                        re[point, 1] = data[i * width + j, 1];
                    }
                    else {
                        re[point, 0] = 0;
                        re[point, 1] = 0;
                    }
                }
            return re;
        }
        // yet another override method of padding
        private byte[] padding(byte[] data, int width, int height, int toWidth, int toHeight)
        {
            byte[] re = new byte[toWidth * toHeight];
            for (int i = 0; i < toHeight; ++i)
                for (int j = 0; j < toWidth; ++j)
                {
                    int point = i * toWidth + j;
                    re[point] = 0;
                    if (i >= 0 && i < height && j >= 0 && j < width)
                    {
                        re[point] = data[i * width + j];
                    }
                    else {
                        re[point] = 0;
                    }
                }
            return re;
        }

        private double[,] translate(double[,] data, int width, int height) {
            double[,] re = new double[width * height, 2];
            for (int i = 0; i < height; ++i)
                for (int j = 0; j < width; ++j)
                {
                    int point = i * width + j;
                    re[point, 0] = data[point, 0];
                    re[point, 1] = data[point, 1];
                    if ((i + j) % 2 == 1)
                    {
                        int now = (int)re[point, 0];
                        if (now >= 128)
                        {
                            re[point, 0] -= 128;
                        }
                        else {
                            re[point, 0] += 128;
                        }
                    }
                }
            return re;
        }

        // filter dft
        /*private void filter2d_freq(double[] srcData, int level, double[] filter) {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            // padding the image and filter
            double[,] tempData = padding(srcData, width, height, width * 2, height * 2);
            double[,] filterData = padding(filter, level, level, width * 2, height * 2);
            // translate image
            tempData = translate(tempData, width * 2, height * 2);
            width *= 2;
            height *= 2;
            // build fourier
            double[,] imageFourier = dft2d(tempData, false, width, height);
            // showFourier(imageFourier, width, height);
            // dft2d(imageFourier, true, width, height);
            double[,] filterFourier = dft2d(filterData, false, width, height);
            // showFourier(filterFourier, width, height);
            double[,] result = new double[width * height, 2];
            for (int i = 0; i < height; ++i) {
                for (int j = 0; j < width; ++j) {
                    var point = i * width + j;
                    result[point, 0] = imageFourier[point, 0] * filterFourier[point, 0] -
                        imageFourier[point, 1] * filterFourier[point, 1];
                    result[point, 1] = imageFourier[point, 1] * filterFourier[point, 0] +
                        imageFourier[point, 0] * filterFourier[point, 1];
                    // result[point, 0] = imageFourier[point, 0];
                    // result[point, 1] = imageFourier[point, 1];
                }
            }
            // write the new image
            // result = 
               dft2d(result, true, width, height);
            // postProcess(result, width, height);
        }*/

        // filter fft
        private void filter2d_freq(double[] srcData, int level, double[] filter)
        {
            // width and height of the image
            int width = img.Width;
            int height = img.Height;
            
            // another padding for fft
            int paddingWidth = cal2times(width);
            int paddingHeight = cal2times(height);
            // padding the image and filter
            double[,] tempData = padding(srcData, width, height, paddingWidth, paddingHeight);
            double[,] filterData = padding(filter, level, level, paddingWidth, paddingHeight);
            // translate image
            tempData = translate(tempData, paddingWidth, paddingHeight);
            /*for (int i = 0; i < height; ++i) {
                for (int j = 0; j < width; ++j) {
                    Console.Write(filterData[i * width + j, 0] + ";");
                }
                Console.Write('\n');
            }*/
            // build fourier
            double[,] imageFourier = fft2d(tempData, false, paddingWidth, paddingHeight);
            // showFourier(imageFourier, width, height);
            // dft2d(imageFourier, true, width, height);
            double[,] filterFourier = fft2d(filterData, false, paddingWidth, paddingHeight);
            // showFourier(filterFourier, width, height);
            double[,] result = new double[paddingWidth * paddingHeight, 2];
            for (int i = 0; i < paddingHeight; ++i)
            {
                for (int j = 0; j < paddingWidth; ++j)
                {
                    var point = i * paddingWidth + j;
                    result[point, 0] = imageFourier[point, 0] * filterFourier[point, 0] -
                        imageFourier[point, 1] * filterFourier[point, 1];
                    result[point, 1] = imageFourier[point, 1] * filterFourier[point, 0] +
                        imageFourier[point, 0] * filterFourier[point, 1];
                    // result[point, 0] = imageFourier[point, 0];
                    // result[point, 1] = imageFourier[point, 1];
                }
            }
            // write the new image
            result = fft2d(result, true, paddingWidth, paddingHeight);
            // padding the result
            result = padding(result, paddingWidth, paddingHeight, width, height);
            postProcess(result, width, height);
        }


        // convert byte image to double
        private double[] getDoubleImage(byte[] data, int width, int height) {
            double[] re = new double[width * height];
            for (int i = 0; i < width * height; ++i) {
                re[i] = data[i];
            }
            return re;
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

        // histogram equation
        private void button2_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // histogram equalization
            
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

        // DFT
        private void button6_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // dft
            double[] data = getDoubleImage(srcData, img.Width, img.Height);
            double[,] inputData = new double[img.Width * img.Height, 2];
            for (int i = 0; i < img.Width * img.Height; ++i) {
                inputData[i, 0] = data[i];
                inputData[i, 1] = 0;
            }
            // translate image
            inputData = translate(inputData, img.Width, img.Height);
            double[,] outputData = dft2d(inputData, false, img.Width, img.Height);
            showFourier(outputData, img.Width, img.Height);
        }

        // average filter
        private void button5_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            int level = Int32.Parse(textBox3.Text);
            // calculate the average 
            double[] filter = new double[level * level];
            for (int i = 0; i < level * level; ++i)
            {
                filter[i] = 1.0 / (double)(level * level);
            }

            // filter it
            double[] data = getDoubleImage(srcData, img.Width, img.Height);
            filter2d_freq(data, level, filter);
        }

        // customized 3*3 filter
        private void button7_Click(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            double[] filter = new double[9];
            // get all the customed filter values
            TextBox[] filterBox = new TextBox[9] {
                filter1, filter2, filter3, filter4, filter5, filter6, filter7, filter8, filter9};
            // initialize the filter
            for (int i = 0; i < 9; ++i) {
                filter[i] = Double.Parse(filterBox[i].Text);
            }
            // filter it
            double[] data = getDoubleImage(srcData, img.Width, img.Height);
            filter2d_freq(data, 3, filter);
        }

        // fft
        private void button2_Click_1(object sender, EventArgs e)
        {
            if (pictureBox1.Image == null) return;
            // fft
            double[] data = getDoubleImage(srcData, img.Width, img.Height);
            // padding
            int paddingWidth = cal2times(img.Width);
            int paddingHeight = cal2times(img.Height);
            double[,] inputData = padding(data, img.Width, img.Height, paddingWidth, paddingHeight);

            // translate image
            inputData = translate(inputData, paddingWidth, paddingHeight);
            double[,] outputData = fft2d(inputData, false, paddingWidth, paddingHeight);
            showFourier(outputData, paddingWidth, paddingHeight);
        }
    }
}
