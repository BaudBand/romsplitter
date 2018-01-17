using System;
using System.Collections.Generic;
using System.IO;
using System.Text;


namespace romsplit
{
    class Program
    {
        
        static void Main(string[] args)
        {
            ROMSplitter r = new ROMSplitter(args);
        }

        public class ROMSplitter
        {
            protected byte[] inputData;
            protected string inputFilename;
            protected string inputChip;
            protected int inputSizeFactor;
            protected int outputSizeFactor;
            protected bool splitRequired;
            protected string[] outputFilename = { "", "" };
            protected string outputPath;
            protected int[] validSizes = { 64, 128, 256, 512 };

            public ROMSplitter(string[] args)
            {
                showCredits();
                // Check command line usage
                if (args.Length == 0)
                {
                    showUsage();
                    errorExit(-1);

                }
                // Set filename for use later - e.g. 'MYROM.BIN'
                inputFilename = Path.GetFileName(args[0]);

                outputPath = Path.GetDirectoryName(Path.GetFullPath(args[0]));

                // Try to load the file, on failure, exit
                if (!loadFile(args[0])) errorExit(-1);

                // Check the size makes sense
                if (!checkSize()) errorExit(-1);

                // Show details
                showROMInfo();

                // Ask user if a split required? (HI LO etc)
                splitRequired = wantToSplit();

                // Check output size requirements
                askOutputSize();

                // Setup and check output files
                setupAndConfirm();

                // Do the file processing
                doSplit();
            }

            public bool loadFile(string filename)
            {
                if (!File.Exists(filename))
                {
                    Console.WriteLine("File not found - " + filename);
                    return false;
                }
                try
                {
                    inputData = File.ReadAllBytes(filename);
                    if (inputData.Length == 0)
                    {
                        Console.WriteLine("File was empty.");
                        return false;
                    }
                    return true;
                }
                catch (FieldAccessException e)
                {
                    Console.WriteLine("You do not have permission to read this file.");
                }
                catch (Exception e)
                {
                    Console.WriteLine("An unknown error occured when trying to read file.");
                }
                return false;
            }

            public void errorExit(int code)
            {
                Environment.Exit(code);
            }

            public void showUsage()
            {
                Console.WriteLine("Usage: romsplit filename.bin");
                Console.WriteLine("On execution, you will be prompted with what files you'd like to generate");
            }

            public void showCredits()
            {
                Console.WriteLine("╔═════════════════════════════════════════╗");
                Console.WriteLine("║              BAUDBAND          (c) 2018 ║");
                Console.WriteLine("║             RomSplitter                 ║");
                Console.WriteLine("╚═════════════════════════════════════════╝");
                Console.WriteLine(" ");
            }

            public bool checkSize()
            {
                inputSizeFactor = (inputData.Length * 8) / 1024; // e.g. converts to kbits - examples might be "64" for a 2764 (8KB) or "256" for a 27256 (32KB)
                Console.WriteLine(inputSizeFactor);
                if (Array.Exists(validSizes, e => e == inputSizeFactor))
                {
                    inputChip = "27" + inputSizeFactor;
                    return true;
                }
                Console.WriteLine("Input ROM loaded correctly, however the size was inconsitent with normal ROM packages.");
                Console.WriteLine("Please check your ROM image is complete.");
                return false;
            }

            public void showROMInfo()
            {
                Console.WriteLine("ROM: " + inputFilename.ToUpper() + " appears to be a " + inputChip + " image.");
            }

            public bool wantToSplit()
            {
                Console.WriteLine("");
                Console.WriteLine("Would you like to split this in to an ODD/EVEN pair? Y/N");
                switch (readKey())
                {
                    case "Y":
                    case "y":
                        return true;
                    case "N":
                    case "n":
                        return false;
                    default:
                        return wantToSplit();
                }
            }

            // default Escape handler for questions
            public string readKey()
            {
                ConsoleKeyInfo inKey = Console.ReadKey(true);

                if (inKey.Key == ConsoleKey.Escape)
                    errorExit(-1);
                return inKey.KeyChar.ToString();
            }

            public void askOutputSize()
            {
                // Only provide options that are big enough to hold the finished ROM(s)
                List<int> validOptions = new List<int>();
                int currentSize = inputSizeFactor / (splitRequired ? 2 : 1);
                foreach (int s in validSizes)
                    if (s >= currentSize)
                        validOptions.Add(s);
                int position = 1;

                // Actually ask what the user wants
                Console.WriteLine("");
                Console.WriteLine("Expecting that your target system will be only viewing a size fit for a " + (inputSizeFactor / (splitRequired ? 2 : 1)));
                Console.WriteLine("what chip size will you actually be programming this file to?");
                string key = "Z";
                foreach (int f in validOptions)
                {
                    Console.WriteLine(position + ") 27" + f);
                    position++;
                }
                try
                {
                    key = readKey();
                    int option = Int16.Parse(key);
                    if (option > 0 && option <= validOptions.Count)
                        outputSizeFactor = validOptions[option - 1];
                    else
                    {
                        Console.WriteLine("You selected option " + option + " out of " + validOptions.Count);
                        askOutputSize();
                    }

                }
                catch (FormatException e)
                {
                    Console.WriteLine(key);
                    askOutputSize();
                }
            }

            public string[] getFileNameAndExtension(string fn)
            {
                string[] split = { Path.GetFileNameWithoutExtension(fn), Path.GetExtension(fn) };
                return split;

            }

            public void setupAndConfirm()
            {
                string[] fileNameTemplate = getFileNameAndExtension(inputFilename);
                outputFilename[0] = fileNameTemplate[0] + "_27" + outputSizeFactor + (splitRequired ? "_ODD" : "") + fileNameTemplate[1];
                outputFilename[1] = fileNameTemplate[0] + "_27" + outputSizeFactor + (splitRequired ? "_EVEN" : "") + fileNameTemplate[1];

                if (File.Exists(outputPath + Path.DirectorySeparatorChar + outputFilename[0]) || (splitRequired && File.Exists(outputPath + Path.DirectorySeparatorChar + outputFilename[1])))
                {
                    Console.WriteLine("Output file(s) already exist in " + outputPath + "! Are you sure you want to continue? Y/N");
                    string confirm = readKey();
                    if (confirm != "Y" && confirm != "y")
                        errorExit(0);
                }
            }

            public void doSplit()
            {
                FileStream file1;
                FileStream file2;

                int spinsRequired = outputSizeFactor / (inputSizeFactor / (splitRequired ? 2 : 1));
                int spinCount = 0;
                int count = 0;
                try
                {
                    file1 = new FileStream(outputPath + Path.DirectorySeparatorChar + outputFilename[0], FileMode.OpenOrCreate, FileAccess.Write);

                    while (spinsRequired > spinCount)
                    {
                        count = 0;
                        while (count < inputData.Length)
                        {
                            file1.WriteByte(inputData[count]);
                            count++; count++;
                        }
                        spinCount++;
                    }
                    file1.Close();
                    if (splitRequired)
                    {
                        spinCount = 0;
                        file2 = new FileStream(outputPath + Path.DirectorySeparatorChar + outputFilename[1], FileMode.OpenOrCreate, FileAccess.Write);
                        while (spinsRequired > spinCount)
                        {
                            count = 1;
                            while (count < inputData.Length)
                            {
                                file2.WriteByte(inputData[count]);
                                count++; count++;
                            }
                            spinCount++;
                        }
                        file2.Close();
                    }
                    Console.WriteLine("Finished - output file" + (splitRequired ? "s" : "") + " can be found in " + outputPath);
                    Console.WriteLine(outputFilename[0] + " " + (splitRequired ? " and " + outputFilename[1] : ""));
                    
                }
                catch (Exception e)
                {
                    Console.WriteLine("Error: " + e.Message);
                    Console.ReadKey();
                }
            }
        }
    }
}
