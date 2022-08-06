using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ZLibNet;

namespace PactDeCodeMain
{
    class Program
    {

        static void Main(string[] args)
        {
            //0 1 2 3

            Console.WriteLine("Warning: this porgram only can be used to afa v2!!!");
            Console.WriteLine("Useage: ");
            Console.WriteLine("Export txt From AFA: PactDeCodeMain.exe 0 XXXPact.afa");
            Console.WriteLine("tips:\t only config txt can be export");
            Console.WriteLine("Import txt To AFA: PactDeCodeMain.exe 1");
            Console.WriteLine("tips:\t Pact.afa is the new file which was imported txt ");

            if (args.Length <= 0)
            {
                Console.Write("Press any key to continue");
                Console.ReadKey();
                return;
            }
            int judge = int.Parse(args[0]);

            switch (judge)
            {
                case 0://export txt
                    GetPactFileFromAfa(args[1]);//create pact

                    string[] listPact = FindFile("Pact//config", "*.pact");

                    if (listPact.Length == 0)
                        return;
                    foreach (string i in listPact)
                    {
                        string PactFileName = i;
                        string[] JafFileNameArray = PactFileName.Split('.');
                        string JafFileName = JafFileNameArray[0] + ".jaf";
                        string ShiftJISTxtFileName = JafFileNameArray[0] + ".txt";

                        GetJafFileFromPact(PactFileName);

                        GetShiftJISStringFromJaf(JafFileName, ShiftJISTxtFileName);
                    }
                    break;

                case 1://import txt
                    string[] listTxt = FindFile("Pact//config", "*.txt");

                    if (listTxt.Length == 0)
                        return;
                    foreach (string i in listTxt)
                    {
                        string PactFileName = i;
                        string[] JafFileNameArray = PactFileName.Split('.');
                        string JafFileName = JafFileNameArray[0] + ".jaf";
                        string NewJafFileName = JafFileNameArray[0] + ".new";

                        CreateNewJaf(JafFileName, i, NewJafFileName);
                        CreatePactFileFromNewJaf(NewJafFileName);
                    }

                    CreatAfaFromPact("Pact");//create afa
                    break;

                case 2:
                    GetPactFileFromAfa(args[1]);//create pact
                    break;

                case 3:
                    CreatAfaFromPact("Pact");//create afa
                    break;

                default:
                    Console.WriteLine("error case {0}", judge);
                    break;

            }

            return;
        }

        /// <summary>
        /// find files
        /// </summary>
        /// <param name="sSourcePath"></param>
        /// <returns></returns>
        public static string[] FindFile(string sSourcePath, string type)
        {

            var files = Directory.GetFiles(sSourcePath, type, SearchOption.AllDirectories); // 遍历所有文件

            return files;
        }

        public static void GetPactFileFromAfa(string AfaFileName)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);//.NET Core 在默认情况下是没有注册EncodeProvider，需要我们们手动自己去注册


            FileStream AfaFileStream = new FileStream(AfaFileName, FileMode.Open);
            BinaryReader AfaBinaryReader = new BinaryReader(AfaFileStream, Encoding.ASCII);

            uint afah = AfaBinaryReader.ReadUInt32();//AFAH
            if (afah != 0x48414641)
            {
                Console.WriteLine("error afa");
                Environment.Exit(1);
            }
            uint HeadLength = AfaBinaryReader.ReadUInt32();//HeadLength

            byte[] AlicArch = AfaBinaryReader.ReadBytes(8);//AlicArch

            byte[] AlicArchMagic = new byte[8] { 0x41, 0x6C, 0x69, 0x63, 0x41, 0x72, 0x63, 0x68 };
            if (AlicArch.Equals(AlicArchMagic))
            {
                Console.WriteLine("error afa");
                Environment.Exit(1);
            }

            uint version = AfaBinaryReader.ReadUInt32();//version 2
            if (version != 2)
            {
                Console.WriteLine("the afa version is not 2");
                Environment.Exit(1);
            }

            uint unknow = AfaBinaryReader.ReadUInt32();//unknow 1

            uint dataOffset = AfaBinaryReader.ReadUInt32();//dataOffset 2

            uint info = AfaBinaryReader.ReadUInt32();//INFO
            if (info != 0x4F464E49)
            {
                Console.WriteLine("error afa");
                Environment.Exit(1);
            }

            uint FileBlockCompressSize = AfaBinaryReader.ReadUInt32();//FileBlockCompressSize

            uint FileBlockReleaseSize = AfaBinaryReader.ReadUInt32();//FileBlockReleaseSize

            uint FileBlockNumber = AfaBinaryReader.ReadUInt32();//FileBlockNumber

            byte[] FileCompressBlock = AfaBinaryReader.ReadBytes((int)FileBlockCompressSize - 0x10);
            Stream FileCompressBlockStream = new MemoryStream(FileCompressBlock);
            ZLibStream zstream = new ZLibStream(FileCompressBlockStream, CompressionMode.Decompress);
            BinaryReader index = new BinaryReader(zstream);
            

            Entry[] entrys = new Entry[FileBlockNumber];
            
            for (int i = 0; i < FileBlockNumber; i++)
            {
                uint nameLength = index.ReadUInt32();//nameLength
                uint paddLength = index.ReadUInt32();//paddLength

                var name_buf = new byte[0x40];
                index.Read(name_buf, 0, (int)paddLength);

                Encoding ei = Encoding.GetEncoding(932);
                var name = ei.GetString(name_buf, 0, (int)nameLength);


                Console.WriteLine(name);
                Console.WriteLine("{0}", index.ReadUInt32());//Unknow1
                Console.WriteLine("{0}", index.ReadUInt32());//Unknow2

                entrys[i] = new Entry(name, index.ReadUInt32(), index.ReadUInt32());


            }
            zstream.Close();
            FileCompressBlockStream.Close();
            index.Close();

            AfaBinaryReader.ReadBytes((int)dataOffset - (int)AfaBinaryReader.BaseStream.Position);

            AfaBinaryReader.ReadBytes(4);//DATA
            

            UInt32 Datalength = AfaBinaryReader.ReadUInt32();//trueDataLength + 0xC

            
            for (int i = 0; i < FileBlockNumber; i++)
            {
                string FileDirIn = "Pact/" + entrys[i].Name;
                string [] pathArray = FileDirIn.Split('/');
                string path = "";
                for (int j = 0; j < pathArray.Length - 1; j++)
                {
                    path += pathArray[j]+"/";
                }

                        
                if (!Directory.Exists(FileDirIn))
                {
                    Directory.CreateDirectory(path);
                }
                        
                FileStream PactFileStream = new FileStream(FileDirIn, FileMode.CreateNew);
                BinaryWriter PactBinaryReader = new BinaryWriter(PactFileStream, Encoding.ASCII);

                PactBinaryReader.Write(AfaBinaryReader.ReadBytes((int)entrys[i].DataSize));

                PactBinaryReader.Close();
                PactFileStream.Close();
                        

            }


            AfaBinaryReader.Close();
            AfaFileStream.Close();
        }

        public static void CreatAfaFromPact(string PactFileDir)
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);//.NET Core 在默认情况下是没有注册EncodeProvider，需要我们们手动自己去注册

            string[] listPact = FindFile(PactFileDir, "*.pact");
            AfaFileNameBlock[] afaFileNameBlocks = new AfaFileNameBlock[listPact.Length];

            if (File.Exists("data1.dat"))
            {
                File.Delete("data1.dat");
            }

            FileStream AfaFileStream = new FileStream("data1.dat", FileMode.CreateNew);
            BinaryWriter AfaBinaryWriter = new BinaryWriter(AfaFileStream, Encoding.ASCII);

            Encoding ei = Encoding.GetEncoding(932);

            uint dataAllSize = 0;
            for (int i = 0; i < listPact.Length; i++)
            {
                afaFileNameBlocks[i] = new AfaFileNameBlock();
                uint partOfFileSize = (uint)GetFileSize(listPact[i]);
                dataAllSize += partOfFileSize;

                afaFileNameBlocks[i].DataSize = partOfFileSize;//size

                FileInfo fileInfo = new FileInfo(listPact[i]);

                string[] listPactApart = listPact[i].Split("\\");
                string trueFileName = "";
                int j = 1;
                if (listPactApart.Length > 2)
                {
                    for (; j < listPactApart.Length - 1; j++)
                    {
                        trueFileName += listPactApart[j] + "\\";

                    }
                }
                trueFileName += listPactApart[j];
                byte[] fileNameByteTemple = ei.GetBytes(trueFileName);
                afaFileNameBlocks[i].TrueNameLength = (uint)fileNameByteTemple.Length;//trueLength

                uint paddLength = PaddLength((uint)fileNameByteTemple.Length);
                afaFileNameBlocks[i].PaddNameLength = paddLength;//paddlength

                

                byte[] fileNameByteTemple2 = new byte[paddLength];

                Array.Copy(fileNameByteTemple, fileNameByteTemple2, fileNameByteTemple.Length);
                afaFileNameBlocks[i].PactFileName = fileNameByteTemple2;//name

            }

            dataAllSize += 0x8;

            byte[] dataSign = new byte[] { 0x44, 0x41, 0x54, 0x41 };

            AfaBinaryWriter.Write(dataSign);
            AfaBinaryWriter.Write(dataAllSize);


            if (File.Exists("data2.dat"))
            {
                File.Delete("data2.dat");
            }
            FileStream AfaFileStream2 = new FileStream("data2.dat", FileMode.CreateNew);
            BinaryWriter AfaBinaryWriter2 = new BinaryWriter(AfaFileStream2, Encoding.ASCII);


            for (int i = 0; i < listPact.Length; i++)
            {
                FileStream PactFileStream = new FileStream(listPact[i], FileMode.Open);
                BinaryReader PactBinaryReader = new BinaryReader(PactFileStream, Encoding.ASCII);
                afaFileNameBlocks[i].DataOffset = (uint)AfaBinaryWriter.BaseStream.Position;

                AfaBinaryWriter.Write(PactBinaryReader.ReadBytes((int)afaFileNameBlocks[i].DataSize));

                PactBinaryReader.Close();
                PactFileStream.Close();
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].TrueNameLength);
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].PaddNameLength);
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].PactFileName);
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].Unknow1);
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].Unknow2);
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].DataOffset);
                AfaBinaryWriter2.Write(afaFileNameBlocks[i].DataSize);
            }

            AfaBinaryWriter2.Close();
            AfaFileStream2.Close();

            AfaBinaryWriter.Close();
            AfaFileStream.Close();



            if (File.Exists("Pact.afa"))
            {
                File.Delete("Pact.afa");
            }
            FileStream AfaFinalFileStream = new FileStream("Pact.afa", FileMode.CreateNew);
            BinaryWriter AfaFinalBinaryWriter = new BinaryWriter(AfaFinalFileStream, Encoding.ASCII);

            AfaFinalBinaryWriter.Write('A');
            AfaFinalBinaryWriter.Write('F');
            AfaFinalBinaryWriter.Write('A');
            AfaFinalBinaryWriter.Write('H');

            AfaFinalBinaryWriter.Write(0x1C);

            AfaFinalBinaryWriter.Write('A');
            AfaFinalBinaryWriter.Write('l');
            AfaFinalBinaryWriter.Write('i');
            AfaFinalBinaryWriter.Write('c');
            AfaFinalBinaryWriter.Write('A');
            AfaFinalBinaryWriter.Write('r');
            AfaFinalBinaryWriter.Write('c');
            AfaFinalBinaryWriter.Write('h');

            AfaFinalBinaryWriter.Write(0x2);

            AfaFinalBinaryWriter.Write(0x1);

            AfaFinalBinaryWriter.Write(0x100000);

            AfaFinalBinaryWriter.Write('I');
            AfaFinalBinaryWriter.Write('N');
            AfaFinalBinaryWriter.Write('F');
            AfaFinalBinaryWriter.Write('O');


            FileStream data2FileStream = new FileStream("data2.dat", FileMode.Open);
            BinaryReader data2BinaryReader = new BinaryReader(data2FileStream, Encoding.ASCII);
            uint size2 = (uint)GetFileSize("data2.dat");

            byte[] data2 = data2BinaryReader.ReadBytes((int)size2);
            byte[] compressData2 = Compress(data2);


            data2BinaryReader.Close();
            data2FileStream.Close();

            AfaFinalBinaryWriter.Write(compressData2.Length + 0x10);
            AfaFinalBinaryWriter.Write(size2);
            AfaFinalBinaryWriter.Write(listPact.Length);
            AfaFinalBinaryWriter.Write(compressData2);

            int now = (int)AfaFinalBinaryWriter.BaseStream.Position;
            now = 0x100000 - now;
            byte[] attch100000 = Enumerable.Repeat((byte)0xFF, now).ToArray();
            AfaFinalBinaryWriter.Write(attch100000);


            FileStream data1FileStream = new FileStream("data1.dat", FileMode.Open);
            BinaryReader data1BinaryReader = new BinaryReader(data1FileStream, Encoding.ASCII);
            uint size1 = (uint)GetFileSize("data1.dat");

            byte[] data1 = data1BinaryReader.ReadBytes((int)size1);

            data1BinaryReader.Close();
            data1FileStream.Close();

            AfaFinalBinaryWriter.Write(data1);



            AfaFinalBinaryWriter.Close();
            AfaFinalFileStream.Close();

            if (File.Exists("data1.dat"))
            {
                File.Delete("data1.dat");
            }
            if (File.Exists("data2.dat"))
            {
                File.Delete("data2.dat");
            }
        }


        /// <summary>
        /// padd length
        /// </summary>
        /// <param name="TrueLength"></param>
        /// <returns></returns>
        public static uint PaddLength(uint TrueLength)
        {
            uint mul = TrueLength / 4;
            if (TrueLength % 4 == 0)
            {
                return mul * 4;
            }
            return (mul + 1) * 4;
        }

        //v2
        public class AfaFileNameBlock
        {
            public uint TrueNameLength { get; set; }
            public uint PaddNameLength { get; set; }
            public byte [] PactFileName { get; set; }
            public uint Unknow1 { get; set; } //? time 392513024
            public uint Unknow2 { get; set; }//30428123
            public uint DataOffset { get; set; }
            public uint DataSize { get; set; }

            public AfaFileNameBlock()
            {
                Unknow1 = 392513024;
                Unknow2 = 30428123;
            }

            public AfaFileNameBlock(uint TrueLength,uint PaddLength,byte [] FileName,
            uint Time1, uint Time2, uint Offset,uint Size)
            {
                TrueNameLength = TrueLength;
                PaddNameLength = PaddLength;
                PactFileName = FileName;
                Unknow1 = Time1;
                Unknow2 = Time2;
                DataOffset = Offset;
                DataSize = Size;
            }

        }

        public class Entry
        {
            public string Name { get; set; }
            public uint DataOffset { get; set; }
            public uint DataSize { get; set; }

            public Entry()
            {
                
            }

            public Entry(string FileName,uint Offset, uint Size)
            {
                Name = FileName;
                DataOffset = Offset;
                DataSize = Size;
            }

        }


        /// <summary>
        /// release Pact and creat jaf
        /// </summary>
        /// <param name="PactFileName"></param>
        public static void GetJafFileFromPact(string PactFileName)
        {
            string [] JafFileNameArray = PactFileName.Split('.');
            string JafFileName = JafFileNameArray[0] + ".jaf";
            if (!File.Exists(PactFileName))
            {
                Console.WriteLine("file is not exist!");
                Environment.Exit(1);
                return;
            }
            if (File.Exists(JafFileName))
            {
                return;
            }
            FileStream PactfileStream = new FileStream(PactFileName, FileMode.Open);
            BinaryReader PactBinaryReader = new BinaryReader(PactfileStream, Encoding.ASCII);
            int releaseSize = PactBinaryReader.ReadInt32();
            int compressSize = (int)GetFileSize(PactFileName) - 0x4;
            byte[] compressFileStream = new byte[compressSize];
            byte[] releaseFileStream = new byte[releaseSize];
            compressFileStream = PactBinaryReader.ReadBytes((int)compressSize);

            var ms = new MemoryStream(compressFileStream);
            ZLibStream zlibStream = new ZLibStream(ms, CompressionMode.Decompress);
            zlibStream.Read(releaseFileStream, 0, releaseFileStream.Length);

            PactBinaryReader.Close();
            PactfileStream.Close();

            FileStream fileStream = new FileStream(JafFileName, FileMode.CreateNew, FileAccess.Write);
            BinaryWriter binaryWriter = new BinaryWriter(fileStream);

            binaryWriter.Write(releaseFileStream);
            binaryWriter.Close();
            fileStream.Close();
        }

        /// <summary>
        /// compress New create Pact
        /// </summary>
        /// <param name="NewJafFileName"></param>
        public static void CreatePactFileFromNewJaf(string NewJafFileName)
        {
            string[] NewJafFileNameArray = NewJafFileName.Split('.');
            string PactFileName = NewJafFileNameArray[0] + ".pact";
            long releaseSize = GetFileSize(NewJafFileName);

            if (File.Exists(PactFileName))
            {
                File.Delete(PactFileName);
            }

            FileStream NewfileStream = new FileStream(NewJafFileName, FileMode.Open);
            BinaryReader NewBinaryReader = new BinaryReader(NewfileStream, Encoding.ASCII);

            byte[] releaseFileStream = new byte[releaseSize];
            releaseFileStream = NewBinaryReader.ReadBytes((int)releaseSize);

            byte[] compressed = Compress(releaseFileStream);

            NewBinaryReader.Close();
            NewfileStream.Close();

            FileStream fileStream = new FileStream(PactFileName, FileMode.CreateNew, FileAccess.Write);
            BinaryWriter binaryWriter = new BinaryWriter(fileStream);
            binaryWriter.Write((uint)releaseSize);

            binaryWriter.Write(compressed);

            binaryWriter.Close();
            fileStream.Close();
        }

        public static byte[] Compress(byte[] input)
        {
            using (MemoryStream ms = new MemoryStream())
            {
                using (ZLibStream zls = new ZLibStream(ms, CompressionMode.Compress, CompressionLevel.Level9))
                {
                    zls.Write(input, 0, input.Length);
                }
                return ms.ToArray();
            }
        }


        /// <summary>
        /// get filesize
        /// </summary>
        /// <param name="sFullName"></param>
        /// <returns></returns>
        public static long GetFileSize(string sFullName)
        {
            long lSize = 0;
            if (File.Exists(sFullName))
                lSize = new FileInfo(sFullName).Length;
            return lSize;
        }

        /// <summary>
        /// analyse
        /// </summary>
        /// <param name="sFullName"></param>
        /// <param name="ShiftJISTxtFileName"></param>
        public static void GetShiftJISStringFromJaf(string sFullName,string ShiftJISTxtFileName)
        {
            
            if (!File.Exists(sFullName))
            {
                Console.WriteLine("no jaf file!!!");
                Environment.Exit(1);
            }
            Console.WriteLine("{0}", sFullName);
            FileStream JaffileStream = new FileStream(sFullName, FileMode.Open);
            BinaryReader JafBinaryReader = new BinaryReader(JaffileStream, Encoding.ASCII);
            uint FileDateNumbel = JafBinaryReader.ReadUInt32();//7 dword
            if (FileDateNumbel != 7)
            {
                Console.WriteLine("errror file! the begin of file dword is not 0x7");
                return;
            }
            uint unknow = JafBinaryReader.ReadUInt32();//1 dword
            uint unknow2 = JafBinaryReader.ReadUInt32();//number ?
            if (unknow2 > 0)
            {
                uint fountionNumbel = 0;
                while (true)
                {
                    ReadStringAndThrow(JafBinaryReader);

                    Sub_4BF9D0(JafBinaryReader);// >= dword   

                    Sub_4BFBE0(JafBinaryReader);//byte or +string
                    Sub_4BFBE0(JafBinaryReader);//byte or +string
                    Sub_4BFBE0(JafBinaryReader);//byte or +string
                    Sub_4BFBE0(JafBinaryReader);//byte or +string
                    Sub_4BFBE0(JafBinaryReader);//byte or +string
                    Sub_4BFBE0(JafBinaryReader);//byte or +string

                    Sub_4B1710(JafBinaryReader); //?

                    uint select = ReadImportantDword(JafBinaryReader);

                    Console.WriteLine("case:{0}", select);

                    

                    switch (select)
                    {
                        case 0:
                            Sub_492ED0(JafBinaryReader);
                            break;
                        case 1:
                            Sub_495750(JafBinaryReader,ShiftJISTxtFileName); 
                            break;
                        case 2:
                            Sub_4A3000(JafBinaryReader);
                            break;
                        case 3:
                            Sub_4A3000(JafBinaryReader);
                            break;
                        case 8:
                            Sub_4A64F0(JafBinaryReader);
                            break;
                        case 9:
                            Sub_4CBB90(JafBinaryReader);
                            Sub_4BF9D0(JafBinaryReader);
                            break;
                        case 0xA:
                            Sub_5039E0(JafBinaryReader, ShiftJISTxtFileName);
                            break;
                        default:
                            Console.WriteLine("GetShiftJISStringFromJaf unknow case {0}!!!!", select);
                            Console.WriteLine("adress: {0}!!!",JafBinaryReader.BaseStream.Position);
                            Environment.Exit(1);
                            break;
                    }
                    
                    if (++fountionNumbel >= unknow2)
                    {
                        break;
                    }
                    
                }
            }
            Console.WriteLine("last position {0}", JafBinaryReader.BaseStream.Position);
            JafBinaryReader.Close();
            JaffileStream.Close();

        }


        /// <summary>
        /// read dword
        /// </summary>
        /// <param name="read"></param>
        public static void ReadDwordAndThrow(BinaryReader read)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                uint temp = read.ReadUInt32();
                if (temp == 0x100)
                {
                    Console.WriteLine("0x100 postion {0}", read.BaseStream.Position);
                }
            }
            else
            {
                Console.WriteLine("ReadDwordAndThrow read over!!!");
                Environment.Exit(1);
            }
        }
        public static void ReadDwordAndThrow(BinaryReader read,  ArrayList code)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                uint temp = read.ReadUInt32();
                if (temp == 0x100)
                {
                    Console.WriteLine("0x100 postion {0}", read.BaseStream.Position);
                }
                code.Add(temp);
            }
            else
            {
                Console.WriteLine("ReadDwordAndThrow read over!!!");
                Environment.Exit(1);
            }
        }

        /// <summary>
        /// read string
        /// </summary>
        /// <param name="read"></param>
        public static void ReadStringAndThrow(BinaryReader read)
        {
            while (true)
            {
                if (read.BaseStream.Position < read.BaseStream.Length)
                {
                    byte code = read.ReadByte();
                    if (code == '\0')
                    {
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("ReadStringAndThrow read over!!!");
                    Environment.Exit(1);
                }
            }
        }
        public static void ReadStringAndThrow(BinaryReader read,   ArrayList code)
        {
            while (true)
            {
                if (read.BaseStream.Position < read.BaseStream.Length)
                {
                    byte messageByte = read.ReadByte();
                    code.Add(messageByte);
                    if (messageByte == '\0')
                    {
                        break;
                    }
                }
                else
                {
                    Console.WriteLine("ReadStringAndThrow read over!!!");
                    Environment.Exit(1);
                }
            }
        }

        /// <summary>
        /// read byte
        /// </summary>
        /// <param name="read"></param>
        public static void ReadByteAndThrow(BinaryReader read)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                read.ReadByte();
            }
            else
            {
                Console.WriteLine("ReadByte read over!!!");
                Environment.Exit(1);
            }
            
        }
        public static void ReadByteAndThrow(BinaryReader read,   ArrayList code)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                byte A = read.ReadByte();
                code.Add(A);
            }
            else
            {
                Console.WriteLine("ReadByte read over!!!");
                Environment.Exit(1);
            }

        }

        /// <summary>
        /// return dword
        /// </summary>
        /// <param name="read"></param>
        /// <returns></returns>
        public static uint ReadImportantDword(BinaryReader read)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                return read.ReadUInt32();
            }
            else
            {
                Console.WriteLine("ReadImportantDword read over!!!");
                Environment.Exit(1);
                return 1;
            }
            
        }
        public static uint ReadImportantDword(BinaryReader read,   ArrayList code)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                uint imp = read.ReadUInt32();
                code.Add(imp);
                return imp;
            }
            else
            {
                Console.WriteLine("ReadImportantDword read over!!!");
                Environment.Exit(1);
                return 1;
            }

        }

        /// <summary>
        /// return byte
        /// </summary>
        /// <param name="read"></param>
        /// <returns></returns>
        public static byte ReadImportantByte(BinaryReader read)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                return read.ReadByte();
            }
            else
            {
                Console.WriteLine("ReadImportantDword read over!!!");
                Environment.Exit(1);
                return 1;
            }

        }
        public static byte ReadImportantByte(BinaryReader read,   ArrayList code)
        {
            if (read.BaseStream.Position < read.BaseStream.Length)
            {
                byte imp = read.ReadByte();
                code.Add(imp);
                return imp;
            }
            else
            {
                Console.WriteLine("ReadImportantDword read over!!!");
                Environment.Exit(1);
                return 1;
            }

        }

        /// <summary>
        /// return string
        /// </summary>
        /// <param name="read"></param>
        /// <returns></returns>
        public static ArrayList ReadShiftJISConfigString(BinaryReader read)
        {
            ArrayList message = new ArrayList();
            while (true)
            {
                if (read.BaseStream.Position < read.BaseStream.Length)
                {
                    byte code = read.ReadByte();
                    byte judge = 0;
                    if (code == judge)
                    {
                        break;
                    }
                    message.Add(code);
                }
                else
                {
                    Console.WriteLine("ReadShiftJISConfigString read over!!!");
                    Environment.Exit(1);
                }   
            }
            return message;
        }
        public static ArrayList ReturnStringFromTxt(BinaryReader read)
        {
            ArrayList message = new ArrayList();
            while (true)
            {
                if (read.BaseStream.Position < read.BaseStream.Length)
                {
                    byte code = read.ReadByte();
                    byte judge = 0xA;
                    if (code == judge)
                    {
                        break;
                    }
                    message.Add(code);
                }
                else
                {
                    Console.WriteLine("ReadShiftJISConfigString read over!!!");
                    Environment.Exit(1);
                }
            }
            return message;
        }

        /// <summary>
        /// attch of GetShiftJISStringFromJaf
        /// </summary>
        /// <param name="read"></param>
        public static void  Sub_4BFBE0(BinaryReader read)
        {
            byte v3 = ReadImportantByte(read);
            byte v2 = 1;
            if (v3 == v2)
            {
                ReadStringAndThrow(read);
            }
        }
        public static void Sub_4BFBE0(BinaryReader read,   ArrayList code)
        {
            byte v3 = ReadImportantByte(read,  code);
            byte v2 = 1;
            if (v3 == v2)
            {
                ReadStringAndThrow(read,   code);
            }
        }



        /// <summary>
        /// attch of Sub_4B1710
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_4B3870(BinaryReader read)
        {
            uint v4 = read.ReadUInt32();
            uint v5 = 0;
            if (v4 > 0)
            {
                while (true)
                {
                    //Sub_4B40B0
                    ReadStringAndThrow(read);//v9 string
                    ReadStringAndThrow(read);//v6 string

                    //Sub_4FE310
                    ReadDwordAndThrow(read);//v3 dword
                    ReadDwordAndThrow(read);//v6 dword

                    //Sub_4B40B0
                    ReadDwordAndThrow(read);//v4 dword
                    ReadDwordAndThrow(read);//v5 dword

                    ++v5;
                    if (v5 >= v4)
                    {
                        break;
                    }

                }
            }
            uint v12;
            uint v8 = read.ReadUInt32();
            uint v15 = 0;
            uint v16 = v8;
            if (v8 > 0)
            {
                for (uint i = 0; ; i += 0x10)
                {
                    v12 = read.ReadUInt32();
                    if (v12 > 0)
                    {
                        uint v13 = 0;
                        while (true)
                        {
                            //Sub_4B40B0
                            ReadStringAndThrow(read);//v9 string
                            ReadStringAndThrow(read);//v6 string

                            //Sub_4FE310
                            ReadDwordAndThrow(read);//v3 dword
                            ReadDwordAndThrow(read);//v6 dword

                            //Sub_4B40B0
                            ReadDwordAndThrow(read);//v4 dword
                            ReadDwordAndThrow(read);//v5 dword

                            if (++v13 >= v12)
                            {
                                break;
                            }
                        }
                    }
                    if (++v15 >= v16)
                    {
                        break;
                    }
                }
            }

        }
        public static void Sub_4B3870(BinaryReader read,   ArrayList code)
        {
            uint v4 = read.ReadUInt32();
            code.Add(v4);
            uint v5 = 0;
            if (v4 > 0)
            {
                while (true)
                {
                    //Sub_4B40B0
                    ReadStringAndThrow(read,  code);//v9 string
                    ReadStringAndThrow(read,   code);//v6 string

                    //Sub_4FE310
                    ReadDwordAndThrow(read,   code);//v3 dword
                    ReadDwordAndThrow(read,   code);//v6 dword

                    //Sub_4B40B0
                    ReadDwordAndThrow(read,   code);//v4 dword
                    ReadDwordAndThrow(read,   code);//v5 dword

                    ++v5;
                    if (v5 >= v4)
                    {
                        break;
                    }

                }
            }
            uint v12;
            uint v8 = read.ReadUInt32();
            code.Add(v8);
            uint v15 = 0;
            uint v16 = v8;
            if (v8 > 0)
            {
                for (uint i = 0; ; i += 0x10)
                {
                    v12 = read.ReadUInt32();
                    code.Add(v12);
                    if (v12 > 0)
                    {
                        uint v13 = 0;
                        while (true)
                        {
                            //Sub_4B40B0
                            ReadStringAndThrow(read,  code);//v9 string
                            ReadStringAndThrow(read,   code);//v6 string

                            //Sub_4FE310
                            ReadDwordAndThrow(read,   code);//v3 dword
                            ReadDwordAndThrow(read,   code);//v6 dword

                            //Sub_4B40B0
                            ReadDwordAndThrow(read,   code);//v4 dword
                            ReadDwordAndThrow(read,   code);//v5 dword

                            if (++v13 >= v12)
                            {
                                break;
                            }
                        }
                    }
                    if (++v15 >= v16)
                    {
                        break;
                    }
                }
            }

        }


        public static void Sub_4B1710(BinaryReader read)
        {
            uint v3 = 0;
            ReadDwordAndThrow(read);//a1+8 dword

            ReadByteAndThrow(read);//a1 + 0xC byte
            ReadByteAndThrow(read);//a1 + 0xD byte

            ReadDwordAndThrow(read);//a1 + 0x10 dword
            ReadDwordAndThrow(read);//a1 + 0x14 dword

            ReadDwordAndThrow(read);//a1 + 0x18 dword
            ReadDwordAndThrow(read);//a1 + 0x1C dword

            ReadByteAndThrow(read);//a1 + 0x20 byte
            ReadByteAndThrow(read);//a1 + 0x21 byte

            ReadDwordAndThrow(read);//a1 + 0x24 dword
            ReadDwordAndThrow(read);//a1 + 0x2C dword
            ReadDwordAndThrow(read);//a1 + 0x30 dword
            ReadDwordAndThrow(read);//a1 + 0x34 dword
            ReadDwordAndThrow(read);//a1 + 0x38 dword
            ReadDwordAndThrow(read);//a1 + 0x3C dword
            ReadDwordAndThrow(read);//a1 + 0x40 dword
            ReadDwordAndThrow(read);//a1 + 0x44 dword

            ReadDwordAndThrow(read);//a1 + 0x48 dword
            ReadDwordAndThrow(read);//a1 + 0x4C dword
            ReadDwordAndThrow(read);//a1 + 0x50 dword
            ReadDwordAndThrow(read);//a1 + 0x54 dword
            ReadDwordAndThrow(read);//a1 + 0x58 dword

            ReadDwordAndThrow(read);//a1 + 0x5C dword
            ReadDwordAndThrow(read);//a1 + 0x60 dword
            ReadDwordAndThrow(read);//a1 + 0x64 dword
            ReadDwordAndThrow(read);//a1 + 0x68 dword
            ReadDwordAndThrow(read);//a1 + 0x6C dword
            ReadDwordAndThrow(read);//a1 + 0x70 dword
            ReadDwordAndThrow(read);//a1 + 0x74 dword
            ReadDwordAndThrow(read);//a1 + 0x78 dword
            ReadDwordAndThrow(read);//a1 + 0x7C dword
            ReadDwordAndThrow(read);//a1 + 0x80 dword
            ReadDwordAndThrow(read);//a1 + 0x84 dword

            ReadByteAndThrow(read);//a1 + 0x88 byte

            ReadDwordAndThrow(read);//a1 + 0x8C dword   3
            ReadDwordAndThrow(read);//a1 + 0x90 dword   3B9AF179

            uint i = ReadImportantDword(read);//dword
            if (i > 0)
            {
                while (true)//a1 + 0x94 dword
                {
                    ReadDwordAndThrow(read);
                    if (++v3 >= i)
                    {
                        break;
                    }
                }
            }
            //label_41
            ReadDwordAndThrow(read);//a1 + 0xA4 dword
            ReadDwordAndThrow(read);//a1 + 0xA8 dword

            Sub_4B3870(read);//a1 + 0x2C8 ddword
            Sub_4B3870(read);//a1 + 0x2EC ddword

            Sub_4B3870(read);//a1 + 0x310 ddword
            Sub_4B3870(read);//a1 + 0x334 ddword
            Sub_4B3870(read);//a1 + 0x358 ddword
            Sub_4B3870(read);//a1 + 0x37C ddword
            Sub_4B3870(read);//a1 + 0x3A0 ddword
            Sub_4B3870(read);//a1 + 0x3C4 ddword
            Sub_4B3870(read);//a1 + 0x3E8 ddword
            Sub_4B3870(read);//a1 + 0xEC ddword
            Sub_4B3870(read);//a1 + 0x110 ddword

            ReadDwordAndThrow(read);//a1 + 0x134 dword
            ReadDwordAndThrow(read);//a1 + 0x138 dword

            Sub_4B3870(read);//a1 + 0x13C ddword
            Sub_4B3870(read);//a1 + 0x160 ddword
            Sub_4B3870(read);//a1 + 0x184 ddword
            Sub_4B3870(read);//a1 + 0x1A8 ddword
            Sub_4B3870(read);//a1 + 0x1CC ddword

            Sub_4B3870(read);//a1 + 0x1F0 ddword

            Sub_4B3870(read);//a1 + 0x214 ddword
            Sub_4B3870(read);//a1 + 0x238 ddword
            Sub_4B3870(read);//a1 + 0x25C ddword
            Sub_4B3870(read);//a1 + 0x280 ddword

            Sub_4B3870(read);//a1 + 0x2A4 ddword
            i = ReadImportantDword(read);
            uint v26 = 0;
            if (i > 0)
            {
                while (true)//a1 + 0x94 dword
                {
                    ReadDwordAndThrow(read);
                    if (++v26 >= i)
                    {
                        break;
                    }
                }
            }
            i = ReadImportantDword(read);
            uint v27 = 0;
            if (i > 0)
            {
                while (true)//a1 + 0x94 dword
                {
                    ReadDwordAndThrow(read);
                    if (++v27 >= i)
                    {
                        break;
                    }
                }
            }

            ReadByteAndThrow(read);//a1 + 0x4D4 byte
            ReadByteAndThrow(read);//a1 + 0x4D5 byte
            ReadByteAndThrow(read);//a1 + 0x4D6 byte
            ReadByteAndThrow(read);//a1 + 0x4D7 byte
            ReadByteAndThrow(read);//a1 + 0x4F8 byte

            for (i = 0; i < 0x70; i+= 0x1C)
            {
                ReadStringAndThrow(read);
            }

            uint v30 = 0;
            while (true)
            {
                ReadDwordAndThrow(read);//dword
                v30 += 4;
                if (v30 >= 0x10)
                {
                    break;
                }
            }
        }
        public static void Sub_4B1710(BinaryReader read,   ArrayList code)
        {
            uint v3 = 0;
            ReadDwordAndThrow(read,  code);//a1+8 dword

            ReadByteAndThrow(read,   code);//a1 + 0xC byte
            ReadByteAndThrow(read,   code);//a1 + 0xD byte

            ReadDwordAndThrow(read,   code);//a1 + 0x10 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x14 dword

            ReadDwordAndThrow(read,   code);//a1 + 0x18 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x1C dword

            ReadByteAndThrow(read,   code);//a1 + 0x20 byte
            ReadByteAndThrow(read,   code);//a1 + 0x21 byte

            ReadDwordAndThrow(read,   code);//a1 + 0x24 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x2C dword
            ReadDwordAndThrow(read,   code);//a1 + 0x30 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x34 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x38 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x3C dword
            ReadDwordAndThrow(read,   code);//a1 + 0x40 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x44 dword

            ReadDwordAndThrow(read,   code);//a1 + 0x48 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x4C dword
            ReadDwordAndThrow(read,   code);//a1 + 0x50 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x54 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x58 dword

            ReadDwordAndThrow(read,   code);//a1 + 0x5C dword
            ReadDwordAndThrow(read,   code);//a1 + 0x60 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x64 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x68 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x6C dword
            ReadDwordAndThrow(read,   code);//a1 + 0x70 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x74 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x78 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x7C dword
            ReadDwordAndThrow(read,   code);//a1 + 0x80 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x84 dword

            ReadByteAndThrow(read,   code);//a1 + 0x88 byte

            ReadDwordAndThrow(read,   code);//a1 + 0x8C dword   3
            ReadDwordAndThrow(read,   code);//a1 + 0x90 dword   3B9AF179

            uint i = ReadImportantDword(read,   code);//dword
            if (i > 0)
            {
                while (true)//a1 + 0x94 dword
                {
                    ReadDwordAndThrow(read,   code);
                    if (++v3 >= i)
                    {
                        break;
                    }
                }
            }
            //label_41
            ReadDwordAndThrow(read,   code);//a1 + 0xA4 dword
            ReadDwordAndThrow(read,   code);//a1 + 0xA8 dword

            Sub_4B3870(read,   code);//a1 + 0x2C8 ddword
            Sub_4B3870(read,   code);//a1 + 0x2EC ddword

            Sub_4B3870(read,   code);//a1 + 0x310 ddword
            Sub_4B3870(read,   code);//a1 + 0x334 ddword
            Sub_4B3870(read,   code);//a1 + 0x358 ddword
            Sub_4B3870(read,   code);//a1 + 0x37C ddword
            Sub_4B3870(read,   code);//a1 + 0x3A0 ddword
            Sub_4B3870(read,   code);//a1 + 0x3C4 ddword
            Sub_4B3870(read,   code);//a1 + 0x3E8 ddword
            Sub_4B3870(read,   code);//a1 + 0xEC ddword
            Sub_4B3870(read,   code);//a1 + 0x110 ddword

            ReadDwordAndThrow(read,   code);//a1 + 0x134 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x138 dword

            Sub_4B3870(read,   code);//a1 + 0x13C ddword
            Sub_4B3870(read,   code);//a1 + 0x160 ddword
            Sub_4B3870(read,   code);//a1 + 0x184 ddword
            Sub_4B3870(read,   code);//a1 + 0x1A8 ddword
            Sub_4B3870(read,   code);//a1 + 0x1CC ddword

            Sub_4B3870(read,   code);//a1 + 0x1F0 ddword

            Sub_4B3870(read,   code);//a1 + 0x214 ddword
            Sub_4B3870(read,   code);//a1 + 0x238 ddword
            Sub_4B3870(read,   code);//a1 + 0x25C ddword
            Sub_4B3870(read,   code);//a1 + 0x280 ddword

            Sub_4B3870(read,   code);//a1 + 0x2A4 ddword
            i = ReadImportantDword(read,   code);
            uint v26 = 0;
            if (i > 0)
            {
                while (true)//a1 + 0x94 dword
                {
                    ReadDwordAndThrow(read,   code);
                    if (++v26 >= i)
                    {
                        break;
                    }
                }
            }
            i = ReadImportantDword(read,   code);
            uint v27 = 0;
            if (i > 0)
            {
                while (true)//a1 + 0x94 dword
                {
                    ReadDwordAndThrow(read,   code);
                    if (++v27 >= i)
                    {
                        break;
                    }
                }
            }

            ReadByteAndThrow(read,   code);//a1 + 0x4D4 byte
            ReadByteAndThrow(read,   code);//a1 + 0x4D5 byte
            ReadByteAndThrow(read,   code);//a1 + 0x4D6 byte
            ReadByteAndThrow(read,   code);//a1 + 0x4D7 byte
            ReadByteAndThrow(read,   code);//a1 + 0x4F8 byte

            for (i = 0; i < 0x70; i += 0x1C)
            {
                ReadStringAndThrow(read,   code);
            }

            uint v30 = 0;
            while (true)
            {
                ReadDwordAndThrow(read,   code);//dword
                v30 += 4;
                if (v30 >= 0x10)
                {
                    break;
                }
            }
        }


        /// <summary>
        /// case 0
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_492ED0(BinaryReader read)
        {
            ReadDwordAndThrow(read);//a2 + 4 dword
            ReadDwordAndThrow(read);//a2 + 8 dword

            ReadByteAndThrow(read);// ++a1[1] byte

            ReadDwordAndThrow(read);//a2 + 0x10 dword
            ReadDwordAndThrow(read);//a2 + 0x14 dword
            ReadDwordAndThrow(read);//a2 + 0x18 dword
            ReadDwordAndThrow(read);//v31 dword
            ReadDwordAndThrow(read);//v32 dword
            ReadDwordAndThrow(read);//v34 dword
            ReadDwordAndThrow(read);//v29 dword
            ReadDwordAndThrow(read);//v33 dword
            ReadDwordAndThrow(read);//v26 dword
            ReadDwordAndThrow(read);//v27 dword
            ReadDwordAndThrow(read);//v28 dword
            ReadDwordAndThrow(read);//v30 dword
            ReadDwordAndThrow(read);//v25 dword

            ReadStringAndThrow(read);//v35 string

            ReadStringAndThrow(read);//v36 string

            ReadStringAndThrow(read);//a2 + 0x168 string

            ReadDwordAndThrow(read);//a2 + 0x184 dword

            ReadDwordAndThrow(read);//a2 + 0x188 dword
            ReadDwordAndThrow(read);//a2 + 0x18C dword
        }
        public static void Sub_492ED0(BinaryReader read,   ArrayList code)
        {
            ReadDwordAndThrow(read,  code);//a2 + 4 dword
            ReadDwordAndThrow(read,   code);//a2 + 8 dword

            ReadByteAndThrow(read,   code);// ++a1[1] byte

            ReadDwordAndThrow(read,   code);//a2 + 0x10 dword
            ReadDwordAndThrow(read,   code);//a2 + 0x14 dword
            ReadDwordAndThrow(read,   code);//a2 + 0x18 dword
            ReadDwordAndThrow(read,   code);//v31 dword
            ReadDwordAndThrow(read,   code);//v32 dword
            ReadDwordAndThrow(read,   code);//v34 dword
            ReadDwordAndThrow(read,   code);//v29 dword
            ReadDwordAndThrow(read,   code);//v33 dword
            ReadDwordAndThrow(read,   code);//v26 dword
            ReadDwordAndThrow(read,   code);//v27 dword
            ReadDwordAndThrow(read,   code);//v28 dword
            ReadDwordAndThrow(read,   code);//v30 dword
            ReadDwordAndThrow(read,   code);//v25 dword

            ReadStringAndThrow(read,   code);//v35 string

            ReadStringAndThrow(read,   code);//v36 string

            ReadStringAndThrow(read,   code);//a2 + 0x168 string

            ReadDwordAndThrow(read,   code);//a2 + 0x184 dword

            ReadDwordAndThrow(read,   code);//a2 + 0x188 dword
            ReadDwordAndThrow(read,   code);//a2 + 0x18C dword
        }


        /// <summary>
        /// case 1
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_495750(BinaryReader read ,string ShiftJISTxtFileName)
        {
            ReadDwordAndThrow(read);//a2 + 4 dword
            ReadDwordAndThrow(read);//a2 + 8 dword

            ReadByteAndThrow(read);// ++a1[1] byte

            ReadDwordAndThrow(read);//a2 + 0x10 dword
            ReadDwordAndThrow(read);//a2 + 0x14 dword
            ReadDwordAndThrow(read);//a2 + 0x18 dword

            uint judge256 = ReadImportantDword(read);//v30 dword
            ReadDwordAndThrow(read);//v31 dword
            ReadDwordAndThrow(read);//v33 dword
            ReadDwordAndThrow(read);//v28 dword
            ReadDwordAndThrow(read);//v32 dword

            ReadDwordAndThrow(read);//v25 dword
            ReadDwordAndThrow(read);//v26 dword

            ReadDwordAndThrow(read);//v27 dword
            ReadDwordAndThrow(read);//v29 dword
            ReadDwordAndThrow(read);//v24 dword

            ReadStringAndThrow(read);//v34 string

            ReadStringAndThrow(read);//v35 string  true byte

            ArrayList ShiftJISMessage = ReadShiftJISConfigString(read);//a2 + 0x1D8 string
            if (ShiftJISMessage.Count > 0)
            {
                ShiftJISMessage.ToArray();
                FileStream sw = new FileStream(ShiftJISTxtFileName, FileMode.Append, FileAccess.Write);
                BinaryWriter bw = new BinaryWriter(sw);
                foreach (byte i in ShiftJISMessage)
                {
                    bw.Write(i);
                }
                bw.Write('\n');
                bw.Close();
                sw.Close();
                Console.WriteLine(read.BaseStream.Position);
            }
            ReadDwordAndThrow(read);//a2 + 0x1F4 dword

            ReadByteAndThrow(read);// a2 + 0x1F8 byte
            ReadDwordAndThrow(read);//a2 + 0x1FC dword
            ReadDwordAndThrow(read);//a2 + 0x200 dword
        }
        public static void Sub_495750(BinaryReader read, BinaryReader ShiftJISTxtFileName,   ArrayList code)
        {
            ReadDwordAndThrow(read,  code);//a2 + 4 dword
            ReadDwordAndThrow(read,   code);//a2 + 8 dword

            ReadByteAndThrow(read,   code);// ++a1[1] byte

            ReadDwordAndThrow(read,   code);//a2 + 0x10 dword
            ReadDwordAndThrow(read,   code);//a2 + 0x14 dword
            ReadDwordAndThrow(read,   code);//a2 + 0x18 dword

            uint judge256 = ReadImportantDword(read);//v30 dword
            if (judge256 == 0x100)
            {
                uint temp = 0;
                code.Add(temp);
            }
            else
            {
                code.Add(judge256);
            }
            ReadDwordAndThrow(read,   code);//v31 dword
            ReadDwordAndThrow(read,   code);//v33 dword
            ReadDwordAndThrow(read,   code);//v28 dword
            ReadDwordAndThrow(read,   code);//v32 dword

            ReadDwordAndThrow(read,   code);//v25 dword
            ReadDwordAndThrow(read,   code);//v26 dword

            ReadDwordAndThrow(read,   code);//v27 dword
            ReadDwordAndThrow(read,   code);//v29 dword
            ReadDwordAndThrow(read,   code);//v24 dword

            ReadStringAndThrow(read,   code);//v34 string

            ReadStringAndThrow(read,   code);//v35 string  true byte

            ArrayList ShiftJISMessage = ReadShiftJISConfigString(read);//a2 + 0x1D8 string
            if (ShiftJISMessage.Count > 0)
            {
                ArrayList message = ReturnStringFromTxt(ShiftJISTxtFileName);
                message.ToArray();
                foreach (byte i in message)
                {
                    code.Add(i);
                }
                
            }
            byte tem = 0;
            code.Add(tem);

            ReadDwordAndThrow(read,   code);//a2 + 0x1F4 dword

            ReadByteAndThrow(read,   code);// a2 + 0x1F8 byte
            ReadDwordAndThrow(read,   code);//a2 + 0x1FC dword
            ReadDwordAndThrow(read,   code);//a2 + 0x200 dword
        }

        /// <summary>
        /// case 3
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_4A3000(BinaryReader read)
        {
            ReadDwordAndThrow(read);//a1[15] dword
            ReadDwordAndThrow(read);//a1[16] dword

            ReadDwordAndThrow(read);//a1 + 0x11 dword
            ReadDwordAndThrow(read);//a1 + 0x12 dword
            ReadDwordAndThrow(read);//a1 + 0x13 dword
            ReadDwordAndThrow(read);//a1 + 0x14 dword
            ReadDwordAndThrow(read);//a1 + 0x15 dword
            ReadDwordAndThrow(read);//a1 + 0x16 dword
            ReadDwordAndThrow(read);//a1 + 0x17 dword

            ReadStringAndThrow(read);//v10 string

            ReadStringAndThrow(read);//v13 string
        }
        public static void Sub_4A3000(BinaryReader read,   ArrayList code)
        {
            ReadDwordAndThrow(read,  code);//a1[15] dword
            ReadDwordAndThrow(read,   code);//a1[16] dword

            ReadDwordAndThrow(read,   code);//a1 + 0x11 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x12 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x13 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x14 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x15 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x16 dword
            ReadDwordAndThrow(read,   code);//a1 + 0x17 dword

            ReadStringAndThrow(read,   code);//v10 string

            ReadStringAndThrow(read,   code);//v13 string
        }


        /// <summary>
        /// case 8
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_4A64F0(BinaryReader read)
        {
            ReadDwordAndThrow(read);//a1 + 0x4 dword

            ReadDwordAndThrow(read);//a1 + 0x8 dword
            ReadDwordAndThrow(read);//a1 + 0xC dword

            ReadByteAndThrow(read);//a1 + 0x10 byte

            ReadDwordAndThrow(read);//a1 + 0x14 dword

            ReadDwordAndThrow(read);//a1 + 0x18 dword
            ReadDwordAndThrow(read);//a1 + 0x1C dword
            ReadDwordAndThrow(read);//a1 + 0x20 dword

            ReadDwordAndThrow(read);//a1 + 0x24 dword

        }
        public static void Sub_4A64F0(BinaryReader read,  ArrayList code)
        {
            ReadDwordAndThrow(read,  code);//a1 + 0x4 dword

            ReadDwordAndThrow(read, code);//a1 + 0x8 dword
            ReadDwordAndThrow(read, code);//a1 + 0xC dword

            ReadByteAndThrow(read, code);//a1 + 0x10 byte

            ReadDwordAndThrow(read, code);//a1 + 0x14 dword

            ReadDwordAndThrow(read, code);//a1 + 0x18 dword
            ReadDwordAndThrow(read, code);//a1 + 0x1C dword
            ReadDwordAndThrow(read, code);//a1 + 0x20 dword

            ReadDwordAndThrow(read, code);//a1 + 0x24 dword

        }

        /// <summary>
        /// case 9
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_4CBB90(BinaryReader read)
        {
            uint v6 = ReadImportantDword(read);//v6
            uint v11 = 0;

            while (true)
            {
                ReadDwordAndThrow(read);//v8

                if (++v11 >= v6)
                {
                    break;
                }
            }
        }
        public static void Sub_4CBB90(BinaryReader read, ArrayList code)
        {
            uint v6 = ReadImportantDword(read,code);//v6
            uint v11 = 0;

            while (true)
            {
                ReadDwordAndThrow(read,code);//v8

                if (++v11 >= v6)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// case 0xA
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_5039E0(BinaryReader read, string ShiftJISTxtFileName)
        {
            ReadByteAndThrow(read);//0 byte


            uint v7 = 0;

            uint v6 = ReadImportantDword(read);//v6 4 dword
            if (v6 > 0)
            {
                while (true)
                {
                    uint v8 = ReadImportantDword(read);//B dword
                    Sub_502970(v8, v7, read,ShiftJISTxtFileName);
                    if (++v7 >= v6)
                    {
                        break;
                    }
                }
            }
        }
        public static void Sub_5039E0(BinaryReader read, BinaryReader ShiftJISTxtFileName, ArrayList code)
        {
            ReadByteAndThrow(read,code);//0 byte


            uint v7 = 0;

            uint v6 = ReadImportantDword(read,code);//v6 4 dword
            if (v6 > 0)
            {
                while (true)
                {
                    uint v8 = ReadImportantDword(read,code);//B dword
                    Sub_502970(v8, v7, read, ShiftJISTxtFileName,code);
                    if (++v7 >= v6)
                    {
                        break;
                    }
                }
            }
        }


        /// <summary>
        /// case 0xA attch
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_4F3D00(BinaryReader read)
        {
            ReadStringAndThrow(read);//v15 string


            ReadDwordAndThrow(read);//v10 dword

            ReadDwordAndThrow(read);//v13 dword

            ReadDwordAndThrow(read);//v11 dword
            ReadDwordAndThrow(read);//v14 dword
            ReadDwordAndThrow(read);//v12 dword

        }
        public static void Sub_4F3D00(BinaryReader read, ArrayList code)
        {
            ReadStringAndThrow(read,code);//v15 string


            ReadDwordAndThrow(read, code);//v10 dword

            ReadDwordAndThrow(read, code);//v13 dword

            ReadDwordAndThrow(read, code);//v11 dword
            ReadDwordAndThrow(read, code);//v14 dword
            ReadDwordAndThrow(read, code);//v12 dword

        }


        public static void Sub_4BF9D0(BinaryReader read)
        {
            uint v5 = 0;
            uint v4 = ReadImportantDword(read);//dword
            if (v4 <= 0)
            {
                return;
            }
            while (true)
            {
                ReadStringAndThrow(read);//string
                if (++v5 >= v4)
                {
                    return;
                }
            }
        }
        public static void Sub_4BF9D0(BinaryReader read, ArrayList code)
        {
            uint v5 = 0;
            uint v4 = ReadImportantDword(read,code);//dword
            if (v4 <= 0)
            {
                return;
            }
            while (true)
            {
                ReadStringAndThrow(read,code);//string
                if (++v5 >= v4)
                {
                    return;
                }
            }
        }


        public static void Sub_502970(uint uthis,uint a3, BinaryReader read, string ShiftJISTxtFileName)
        {
            if (a3 <= 3)
            {
                if (uthis != 0xB)
                {
                    switch (uthis)
                    {
                        case 0xD:
                            Sub_50D850(read, ShiftJISTxtFileName);
                            break;
                        case 0x12:
                            Sub_4F5B90(read);
                            break;
                        default:
                            Console.WriteLine("Sub_502970 unknow case {0}!!!!", uthis);
                            Console.WriteLine("adress: {0}!!!", read.BaseStream.Position);
                            Environment.Exit(1);
                            break;
                    }
                }
                else
                    Sub_4F3D00(read);
                
            }
        }
        public static void Sub_502970(uint uthis, uint a3, BinaryReader read, BinaryReader ShiftJISTxtFileName, ArrayList code)
        {
            if (a3 <= 3)
            {
                if (uthis != 0xB)
                {
                    switch (uthis)
                    {
                        case 0xD:
                            Sub_50D850(read, ShiftJISTxtFileName, code);
                            break;
                        case 0x12:
                            Sub_4F5B90(read, code);
                            break;
                        default:
                            Console.WriteLine("Sub_502970 unknow case {0}!!!!", uthis);
                            Console.WriteLine("adress: {0}!!!", read.BaseStream.Position);
                            Environment.Exit(1);
                            break;
                    }
                }
                else
                    Sub_4F3D00(read,code);

            }
        }


        /// <summary>
        /// case 0xD attch
        /// </summary>
        /// <param name="read"></param>
        public static void Sub_50D850(BinaryReader read, string ShiftJISTxtFileName)
        {
            uint judge1 = Sub_5060A0(read);
            Sub_5060A0(read);

            ReadByteAndThrow(read);

            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);

            uint a3 = ReadImportantDword(read);
            uint v8 = a3;
            uint v9 = 0;
            if (a3 > 0)
            {
                while (true)
                {
                    ReadDwordAndThrow(read);
                    ReadDwordAndThrow(read);
                    ReadDwordAndThrow(read);
                    ReadDwordAndThrow(read);
                    a3 = ReadImportantDword(read);
                    if (++v9 >= v8)
                        break;
                }
            }

            if (judge1 == 0x100)
            {
                ArrayList ShiftJISMessage = ReadShiftJISConfigString(read);
                if (ShiftJISMessage.Count != 0)
                {
                    ShiftJISMessage.ToArray();
                    FileStream sw = new FileStream(ShiftJISTxtFileName, FileMode.Append, FileAccess.Write);
                    BinaryWriter bw = new BinaryWriter(sw);
                    foreach (byte i in ShiftJISMessage)
                    {
                        bw.Write(i);
                    }
                    bw.Write('\n');
                    bw.Close();
                    sw.Close();
                    Console.WriteLine(read.BaseStream.Position);
                }
            }
            else
                ReadStringAndThrow(read);

            ReadByteAndThrow(read);
            ReadByteAndThrow(read);
        }
        public static void Sub_50D850(BinaryReader read, BinaryReader ShiftJISTxtFileName, ArrayList code)
        {
            uint judge1 = Sub_5060A0(read,code);
            Sub_5060A0(read,code);

            ReadByteAndThrow(read,code);

            ReadDwordAndThrow(read,code);
            ReadDwordAndThrow(read,code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);

            uint a3 = ReadImportantDword(read, code);
            uint v8 = a3;
            uint v9 = 0;
            if (a3 > 0)
            {
                while (true)
                {
                    ReadDwordAndThrow(read, code);
                    ReadDwordAndThrow(read, code);
                    ReadDwordAndThrow(read, code);
                    ReadDwordAndThrow(read, code);
                    a3 = ReadImportantDword(read, code);
                    if (++v9 >= v8)
                        break;
                }
            }

            if (judge1 == 0x100)
            {
                ArrayList ShiftJISMessage = ReadShiftJISConfigString(read);
                if (ShiftJISMessage.Count != 0)
                {
                    ArrayList message = ReturnStringFromTxt(ShiftJISTxtFileName);
                    message.ToArray();
                    foreach (byte i in message)
                    {
                        code.Add(i);
                    }
                }
                byte tem = 0;
                code.Add(tem);
            }
            else
                ReadStringAndThrow(read,code);

            ReadByteAndThrow(read, code);
            ReadByteAndThrow(read, code);
        }


        /// <summary>
        /// attch of Sub_50D850
        /// </summary>
        /// <param name="read"></param>
        public static uint Sub_5060A0(BinaryReader read)
        {
            uint re = ReadImportantDword(read);

            ReadDwordAndThrow(read);

            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);

            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);

            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            ReadDwordAndThrow(read);
            if (re == 0x100)
            {
                return re;
            }
            else
                return 0;
        }
        public static uint Sub_5060A0(BinaryReader read, ArrayList code)
        {
            uint re = ReadImportantDword(read);
            if (re == 0x100)
                code.Add(0);
            else
                code.Add(re);

            ReadDwordAndThrow(read,code);

            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);

            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);

            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            ReadDwordAndThrow(read, code);
            if (re == 0x100)
            {
                return re;
            }
            else
                return 0;
        }


        public static void Sub_4F5B90(BinaryReader read)
        {
            uint v127 = ReadImportantDword(read);
            uint v5 = v127;
            uint v8;
            uint v131 = 0;
            if (v5 > 0)
            {
                while (true)
                {
                    v8 = ReadImportantDword(read);
                    switch (v8)
                    {
                        case 0:
                        case 1:
                            //2
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 2:
                            break;
                        case 3:
                        case 0x10:
                        case 0x11:
                            //7
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 4:
                        case 6:
                        case 0x12:
                            //8
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 5:
                            //5
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 7:
                        case 8:
                            //14
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);

                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);

                            ReadDwordAndThrow(read);

                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);

                            ReadDwordAndThrow(read);

                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 9:
                            //10
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 0xA:
                            //7
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 0xB:
                        case 0xC:
                        case 0xD:
                        case 0xE:
                            //9
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            break;
                        case 0xF:
                            //4+0.25
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadDwordAndThrow(read);
                            ReadByteAndThrow(read);
                            break;
                        default:
                            break;
                    }

                    if (++v131 >= v127)
                        break;

                }
                ReadDwordAndThrow(read);//v131
                ReadDwordAndThrow(read);//v94
                ReadDwordAndThrow(read);//v99
                ReadDwordAndThrow(read);//v103
            }

        }
        public static void Sub_4F5B90(BinaryReader read, ArrayList code)
        {
            uint v127 = ReadImportantDword(read,code);
            uint v5 = v127;
            uint v8;
            uint v131 = 0;
            if (v5 > 0)
            {
                while (true)
                {
                    v8 = ReadImportantDword(read, code);
                    switch (v8)
                    {
                        case 0:
                        case 1:
                            //2
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 2:
                            break;
                        case 3:
                        case 0x10:
                        case 0x11:
                            //7
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 4:
                        case 6:
                        case 0x12:
                            //8
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 5:
                            //5
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 7:
                        case 8:
                            //14
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);

                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);

                            ReadDwordAndThrow(read, code);

                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);

                            ReadDwordAndThrow(read, code);

                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 9:
                            //10
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 0xA:
                            //7
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 0xB:
                        case 0xC:
                        case 0xD:
                        case 0xE:
                            //9
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            break;
                        case 0xF:
                            //4+0.25
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadDwordAndThrow(read, code);
                            ReadByteAndThrow(read, code);
                            break;
                        default:
                            break;
                    }

                    if (++v131 >= v127)
                        break;

                }
                ReadDwordAndThrow(read, code);//v131
                ReadDwordAndThrow(read, code);//v94
                ReadDwordAndThrow(read, code);//v99
                ReadDwordAndThrow(read, code);//v103
            }

        }


        public static void CreateNewJaf(string JafFileName, string txtName, string NewJafFileName)
        {
            ArrayList code = new ArrayList();
            if (!File.Exists(JafFileName))
            {
                Console.WriteLine("no jaf file!!!");
                Environment.Exit(1);
            }
            Console.WriteLine("{0}", JafFileName);

            FileStream JaffileStream = new FileStream(JafFileName, FileMode.Open);
            BinaryReader JafBinaryReader = new BinaryReader(JaffileStream, Encoding.ASCII);

            FileStream txtfileStream = new FileStream(txtName, FileMode.Open);
            BinaryReader txtBinaryReader = new BinaryReader(txtfileStream, Encoding.ASCII);

            uint FileDateNumbel = JafBinaryReader.ReadUInt32();//7 dword
            code.Add(FileDateNumbel);
            if (FileDateNumbel != 7)
            {
                Console.WriteLine("errror file! the begin of file dword is not 0x7");
                return;
            }
            uint unknow = JafBinaryReader.ReadUInt32();//1 dword
            code.Add(unknow);
            uint unknow2 = JafBinaryReader.ReadUInt32();//number ?
            code.Add(unknow2);

            /**/
            if (unknow2 > 0)
            {
                uint fountionNumbel = 0;
                while (true)
                {
                    ReadStringAndThrow(JafBinaryReader,code);

                    Sub_4BF9D0(JafBinaryReader, code);// >= dword   

                    Sub_4BFBE0(JafBinaryReader, code);//byte or +string
                    Sub_4BFBE0(JafBinaryReader, code);//byte or +string
                    Sub_4BFBE0(JafBinaryReader, code);//byte or +string
                    Sub_4BFBE0(JafBinaryReader, code);//byte or +string
                    Sub_4BFBE0(JafBinaryReader, code);//byte or +string
                    Sub_4BFBE0(JafBinaryReader, code);//byte or +string

                    Sub_4B1710(JafBinaryReader, code); //?

                    uint select = ReadImportantDword(JafBinaryReader, code);

                    Console.WriteLine("case:{0}", select);



                    switch (select)
                    {
                        case 0:
                            Sub_492ED0(JafBinaryReader, code);
                            break;
                        case 1:
                            Sub_495750(JafBinaryReader, txtBinaryReader, code);
                            break;
                        case 2:
                            Sub_4A3000(JafBinaryReader, code);
                            break;
                        case 3:
                            Sub_4A3000(JafBinaryReader, code);
                            break;
                        case 8:
                            Sub_4A64F0(JafBinaryReader, code);
                            break;
                        case 9:
                            Sub_4CBB90(JafBinaryReader, code);
                            Sub_4BF9D0(JafBinaryReader, code);
                            break;
                        case 0xA:
                            Sub_5039E0(JafBinaryReader, txtBinaryReader, code);
                            break;
                        default:
                            Console.WriteLine("GetShiftJISStringFromJaf unknow case {0}!!!!", select);
                            Console.WriteLine("adress: {0}!!!", JafBinaryReader.BaseStream.Position);
                            Environment.Exit(1);
                            break;
                    }

                    if (++fountionNumbel >= unknow2)
                    {
                        break;
                    }

                }
            }
            Console.WriteLine("last position {0}", JafBinaryReader.BaseStream.Position);

            int lastLength = (int)JafBinaryReader.BaseStream.Length - (int)JafBinaryReader.BaseStream.Position;

            byte [] last = JafBinaryReader.ReadBytes(lastLength);
            foreach (byte i in last)
            {
                code.Add(i);
            }
            JafBinaryReader.Close();
            JaffileStream.Close();

            txtBinaryReader.Close();
            txtfileStream.Close();

            FileStream NewJafFileStream = new FileStream(NewJafFileName, FileMode.CreateNew, FileAccess.Write);
            BinaryWriter newBinaryWeader = new BinaryWriter(NewJafFileStream);
            foreach (var j in code)
            {
                if (j.GetType() == typeof(uint))
                {
                    newBinaryWeader.Write((uint)j);
                }
                else if (j.GetType() == typeof(int))
                {
                    newBinaryWeader.Write((int)j);
                }
                else if (j.GetType() == typeof(byte))
                {
                    newBinaryWeader.Write((byte)j);
                }
                
            }
            newBinaryWeader.Close();
            NewJafFileStream.Close();
        }

    }

}
