using Microsoft.ProjectOxford.Face;
using Microsoft.ProjectOxford.Face.Contract;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace FaceInfo {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {
        private FaceServiceClient faceApi;
        private Timer faceTimer;
        private string personGroupId = "myfamily";


        public MainWindow() {

            InitializeComponent();

            Directory.CreateDirectory("c:\\hack\\");
            Directory.GetFiles("c:\\hack\\").ToList().ForEach(f => {
                lst.Items.Insert(0, $"Deleted {f}");
                File.Delete(f);
            }
           );

            //cameras = camera.GetVideoCaptureDevices().ToList();
            faceTimer = new Timer(1111);
            faceTimer.Elapsed += FaceTimer_Elapsed;
            faceApi = new FaceServiceClient(
                "<some id>",
                "https://westeurope.api.cognitive.microsoft.com/face/v1.0"
            );

            CreatePersonsAndTrain();
        }

        private async void FaceTimer_Elapsed(object sender, ElapsedEventArgs e) {
            try {
                await this.Dispatcher.Invoke(async () => {
                    HttpClient client = new HttpClient();
                    var stream = await client.GetStreamAsync(
                        new Uri("http://<camera>/cgi-bin/CGIProxy.fcgi?cmd=snapPicture")
                    );
                    
                    var image = Image.FromStream(stream);
                    var fname = "c:\\hack\\ff" + DateTime.Now.ToFileTimeUtc().ToString() + ".bmp";
                    var fnameout = "c:\\hack\\ff" + DateTime.Now.ToFileTimeUtc().ToString() + ".bmp";
                    image.Save(fname);

                    BitmapImage bi3 = new BitmapImage();
                    bi3.BeginInit();
                    bi3.UriSource = new Uri(fname);
                    bi3.EndInit();
                    input.Stretch = Stretch.Fill;
                    input.Source = bi3;

                    Task<Face[]> task = UploadAndDetectFaces(fname);
                    var faces = await task;
                    if (faces.Count() > 0) {
                        lst.Items.Insert(0, $"Face detected: {fname}");

                        // Only first face for now
                        var dface = faces.First();
                        DisplayFaceProperties(dface);

                        using (Graphics g = Graphics.FromImage(image)) {
                            var facePen = new System.Drawing.Pen(System.Drawing.Color.Green, 3);
                            g.DrawRectangle(facePen, new Rectangle(dface.FaceRectangle.Left, dface.FaceRectangle.Top, dface.FaceRectangle.Width, dface.FaceRectangle.Height));
                            image.Save(@"d:\output.jpg", ImageFormat.Jpeg);
                        }

                        BitmapImage bi4 = new BitmapImage();
                        bi4.BeginInit();
                        bi4.UriSource = new Uri(@"d:\output.jpg");
                        bi4.EndInit();
                        output.Stretch = Stretch.Fill;
                        output.Source = bi4;

                    }
                    else {
                        lst.Items.Insert(0, "No faces detected!");
                    }
                });
            }
            catch { }
        }

        private void DisplayFaceProperties(Face face) {
            try {
                lst.Items.Insert(0, $"ID: {face.FaceId}");
                lst.Items.Insert(0, $"Gender: {face.FaceAttributes.Gender}");
                lst.Items.Insert(0, $"Age: {face.FaceAttributes.Age}");
                lst.Items.Insert(0, $"Emotion: {face.FaceAttributes.Emotion.ToRankedList().First()}");
                lst.Items.Insert(0, $"Bald: {face.FaceAttributes.Hair?.Bald}");
                lst.Items.Insert(0, $"Hair color: {face.FaceAttributes.Hair?.HairColor?.First().Color}");
                lst.Items.Insert(0, $"Moustache: {face.FaceAttributes.FacialHair?.Moustache}");
                lst.Items.Insert(0, $"Sideburns: {face.FaceAttributes.FacialHair?.Sideburns}");
                lst.Items.Insert(0, $"Beard: {face.FaceAttributes.FacialHair?.Beard}");
                lst.Items.Insert(0, $"Occluded forehead: {face.FaceAttributes.Occlusion?.ForeheadOccluded}");
                lst.Items.Insert(0, $"Occluded eyes: {face.FaceAttributes.Occlusion?.EyeOccluded}");
                lst.Items.Insert(0, $"Occluded mouth: {face.FaceAttributes.Occlusion?.MouthOccluded}");
                lst.Items.Insert(0, $"Smile: {face.FaceAttributes.Smile}");
                lst.Items.Insert(0, $"Glasses: {face.FaceAttributes.Glasses}");
                lst.Items.Insert(0, $"Eye makeup : {face.FaceAttributes.Makeup?.EyeMakeup}");
                lst.Items.Insert(0, $"Lip makeup : {face.FaceAttributes.Makeup?.LipMakeup}");
            }
            catch { }
        }

        private void Button_Click(object sender, RoutedEventArgs e) {
            try {
                faceTimer.AutoReset = true;
                faceTimer.Start();
            }
            catch (Exception) { }
            finally { }
        }

        private async void CreatePersonsAndTrain() {
            var path = @"c:\training\richard\";

            // Create an empty PersonGroup
            try {
                await faceApi.DeletePersonGroupAsync(personGroupId);
            }
            catch { }
            await faceApi.CreatePersonGroupAsync(personGroupId, personGroupId);

            // Define Anna
            CreatePersonResult friend1 = await faceApi.CreatePersonAsync(
                // Id of the PersonGroup that the person belonged to
                personGroupId,
                // Name of the person
                "Richard"
            );

            // Directory contains image files of Richard
            string friend1ImageDir = path;

            foreach (string imagePath in Directory.GetFiles(friend1ImageDir, "*.bmp")) {
                using (Stream s = File.OpenRead(imagePath)) {
                    // Detect faces in the image and add to Anna
                    await faceApi.AddPersonFaceAsync(
                        personGroupId, friend1.PersonId, s);
                }
            }

            // Train the myfamily group
            await faceApi.TrainPersonGroupAsync(personGroupId);

            TrainingStatus trainingStatus = null;
            while (true) {
                Console.WriteLine("Training...");
                trainingStatus = await faceApi.GetPersonGroupTrainingStatusAsync(personGroupId);

                if (trainingStatus.Status != Status.Running) {
                    Console.WriteLine("Done!");
                    break;
                }

                await Task.Delay(1000);
            }

        }

        private async Task<Face[]> UploadAndDetectFaces(string imageFilePath) {
            // The list of Face attributes to return.
            IEnumerable<FaceAttributeType> faceAttributes =
                new FaceAttributeType[] { FaceAttributeType.Gender, FaceAttributeType.Occlusion, FaceAttributeType.HeadPose, FaceAttributeType.Age, FaceAttributeType.Smile, FaceAttributeType.Emotion, FaceAttributeType.Glasses, FaceAttributeType.Hair, FaceAttributeType.FacialHair };

            // Call the Face API.
            try {
                using (Stream imageFileStream = File.OpenRead(imageFilePath)) {
                    var faces = await faceApi.DetectAsync(imageFileStream, returnFaceId: true, returnFaceLandmarks: true, returnFaceAttributes: faceAttributes);
                    var faceIds = faces.Select(face => face.FaceId).ToArray();

                    if (faces.Count() > 0) {
                        var results = await faceApi.IdentifyAsync(personGroupId, faceIds);
                        if (results.Count() > 0) {
                            lst.Items.Clear();

                            lst.Items.Insert(0, $"Person: {results.First().Candidates?.First()?.PersonId} ({results.First().Candidates?.First()?.Confidence})");
                            var person = await faceApi.GetPersonAsync("myfamily", results.First().Candidates.First().PersonId);
                            Console.WriteLine($"Identified as {person.Name}");
                            lst.Items.Insert(0, $"Identified as {person.Name}({results.First().Candidates?.First()?.Confidence})");
                        }
                    }

                    return faces;
                }
            }
            // Catch and display Face API errors.
            catch (FaceAPIException f) {
                Console.WriteLine(f.ErrorMessage, f.ErrorCode);
                return new Face[0];
            }
            // Catch and display all other errors.
            catch (Exception e) {
                Console.WriteLine(e.Message, "Error");
                return new Face[0];
            }
        }
    }
}
