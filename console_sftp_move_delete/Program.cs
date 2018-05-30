using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.IO;
using System.Threading;
using Renci.SshNet;        //Search on NuGet for SSH.Net and install
using Renci.SshNet.Sftp;   //Search on NuGet for SSH.Net and install

namespace console_sftp_move_delete
{
    class Program
    {
        static void Main(string[] args)
        {
            //The algorithm:
            //(1) Scan sftp folder, every minute, and save file names ending in .mbz and file sizes.
            //(2) on next minute scan, see if file size changes, if changes do nothing.
            //(3) if file size did NOT change, move file to other folder and delete from sftp folder.

            Console.WriteLine("Starting.....");
            StreamReader connect_info = new StreamReader("./connect_info.txt");
            string host = connect_info.ReadLine();
            string port_string = connect_info.ReadLine();
            string username = connect_info.ReadLine();
            string password = connect_info.ReadLine();
            string srcpath = connect_info.ReadLine();
            string finalpath = connect_info.ReadLine();
            int port = int.Parse(port_string);
            connect_info.Close();


            while (true)
            {
                Console.WriteLine("Connecting sftp.....");
                SftpClient sftpclient = new SftpClient(host, port, username, password);
                sftpclient.Connect();
                sftpclient.ChangeDirectory(srcpath);

                Console.WriteLine("Doing scan 1 before waiting time.....");
                List<SftpFile> old_files = (List<SftpFile>)sftpclient.ListDirectory(srcpath);

                Console.WriteLine("Disconnecting sftp.....");
                sftpclient.Disconnect();

                Dictionary<string, long> old_dict = new Dictionary<string, long>();
                for (int ii = 0; ii < old_files.Count; ii++)
                {
                    if (old_files[ii].Name.Equals(".") == false && old_files[ii].Name.Equals("..") == false)
                    {
                        Console.WriteLine("{0} {1}", old_files[ii].Name, old_files[ii].Length);
                        old_dict.Add(old_files[ii].Name, old_files[ii].Length);
                    }
                }

                for (int ii = 1; ii <= 12; ii++)
                {
                    Console.WriteLine("Sleeping {0} out of 12 for 10 seconds.....", ii);
                    Thread.Sleep(10000);
                }


                Console.WriteLine("Connecting sftp.....");
                sftpclient = new SftpClient(host, port, username, password);
                sftpclient.Connect();
                sftpclient.ChangeDirectory(srcpath);

                Console.WriteLine("Doing scan 2 after waiting time.....");
                List<SftpFile> new_files = (List<SftpFile>)sftpclient.ListDirectory(srcpath);

                Console.WriteLine("Disconnecting sftp.....");
                sftpclient.Disconnect();


                for (int ii = 0; ii < new_files.Count; ii++)
                {
                    if (new_files[ii].Name.Equals(".") == false && new_files[ii].Name.Equals("..") == false)
                    {
                        long old_length = 0;
                        bool skip_it = false;

                        try
                        {
                            old_length = old_dict[new_files[ii].Name];
                        }
                        catch (KeyNotFoundException ex)
                        {
                            Console.WriteLine("Filename {0} not found in old scan, wait till next scan cycle.....", new_files[ii].Name);
                            skip_it = true;
                        }

                        if (skip_it == false)
                        {
                            if (old_length == new_files[ii].Length) //move it and delete it
                            {
                                Console.WriteLine("Working on {0} .....", new_files[ii].Name);
                                string copy_file_name = @".\" + new_files[ii].Name;
                                string src_file_name = srcpath + "/" + new_files[ii].Name;
                                FileStream copy_file_stream = new FileStream(copy_file_name, FileMode.Create);

                                Console.WriteLine("Connecting sftp.....");
                                sftpclient = new SftpClient(host, port, username, password);
                                sftpclient.Connect();
                                sftpclient.ChangeDirectory(srcpath);

                                Console.WriteLine("Downloading {0} .....", new_files[ii].Name);
                                sftpclient.DownloadFile(src_file_name, copy_file_stream);

                                Console.WriteLine("Disconnecting sftp.....");
                                sftpclient.Disconnect();

                                copy_file_stream.Close();

                                string dest_file_name = finalpath + new_files[ii].Name;
                                File.Copy(copy_file_name, dest_file_name);

                                File.Delete(copy_file_name);

                                Console.WriteLine("Connecting sftp.....");
                                sftpclient = new SftpClient(host, port, username, password);
                                sftpclient.Connect();
                                sftpclient.ChangeDirectory(srcpath);

                                Console.WriteLine("Deleting {0} .....", new_files[ii].Name);
                                sftpclient.DeleteFile(src_file_name);

                                Console.WriteLine("Disconnecting sftp.....");
                                sftpclient.Disconnect();
                            }
                        }
                    }
                }

                Console.WriteLine("Clean up before looping again .....");
                old_files.Clear();
                new_files.Clear();
                old_dict.Clear();

            }

        }
    }
}
