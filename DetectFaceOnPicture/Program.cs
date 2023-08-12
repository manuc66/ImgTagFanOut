

using System;
using System.Collections.Generic;
using System.IO;
using OpenCvSharp;

namespace DetectFaceOnPicture
{
    
    public class Program
    {



        public static void Main()
        {
            CascadeClassifier face_cascade = new("./haarcascades/haarcascade_frontalface_default.xml");

            string WorkingFolder = "";
            IEnumerable<string> enumerateFiles = Directory.EnumerateFiles(WorkingFolder, "*.jpg", SearchOption.AllDirectories);



            List<FaceFeature> features = new List<FaceFeature>();
            foreach (string file in enumerateFiles)
            {
                Mat image = GrabFrame(file);

                if  (!DetectFacesInImage(image, face_cascade, features))
                {
                    continue;
                }

                //Mark the detected feature on the original frame
                MarkFeatures(image, features);
                Cv2.ImShow("frame", image);
                Cv2.WaitKey(0);
                if (Cv2.WaitKey(1) == (int)ConsoleKey.Enter)
                    break;
            }
        }

        private static bool DetectFacesInImage(Mat image, CascadeClassifier faceCascade, List<FaceFeature> faceFeatures)
        {
            //Convert to gray scale to improve the image processing
            Mat gray = ConvertGrayScale(image);

            //Detect faces using Cascase classifier
            Rect[] faces = DetectFaces(gray, faceCascade);

            if (image.Empty())
                return false;

            //Loop through detected faces
            foreach (Rect item in faces)
            {
                //Get the region of interest where you can find facial faceFeatures
                Mat face_roi = gray[item];



                //Record the facial faceFeatures in a list
                faceFeatures.Add(new FaceFeature()
                {
                    Face = item,
                });
            }

            return true;
        }

        private static Mat GrabFrame(string filePath)
        {
            return Cv2.ImRead(filePath);
        }

        private static Mat ConvertGrayScale(Mat image)
        {
            Mat gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);
            return gray;
        }

        private static Rect[] DetectFaces(Mat image, CascadeClassifier cascadeClassifier)
        {
            Rect[] faces = cascadeClassifier.DetectMultiScale(image, 1.3, 5);
            return faces;
        }
        

        private static void MarkFeatures(Mat image, List<FaceFeature> faceFeatures)
        {
            foreach (FaceFeature feature in faceFeatures)
            {
                Cv2.Rectangle(image, feature.Face, new Scalar(0, 255, 0), thickness: 1);
                Mat face_region = image[feature.Face];
            }
        }

        public void Release()
        {
            Cv2.DestroyAllWindows();
        }
    }

    class FaceFeature
    {
        public Rect Face { get; set; }

    }
}
