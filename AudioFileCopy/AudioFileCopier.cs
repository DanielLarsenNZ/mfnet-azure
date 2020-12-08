using MediaFoundation;
using MediaFoundation.ReadWrite;
using System;
using System.IO;
using System.Runtime.InteropServices;
using TantaCommon;

namespace AudioFileCopy
{
    public class AudioFileCopier : IDisposable
    {

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// The main form for the application
        /// </summary>
        /// <history>
        ///    01 Nov 18  Cynic - Started
        /// </history>
        private const string DEFAULTLOGDIR = @"C:\Dump\Project Logs";
        private const string APPLICATION_NAME = "TantaAudioFileCopyViaReaderWriter";
        private const string APPLICATION_VERSION = "01.00";

        private const string START_COPY = "Start Copy";
        private const string STOP_COPY = "Stop Copy";

        // sample sound file courtesy of https://archive.org/details/testmp3testfile
        private const string DEFAULT_SOURCE_FILE = @"C:\Dump\SampleAudio_0.4mb.mp3";

        private const string DEFAULT_COPY_SUFFIX = "_TantaCopy";

        // indicates if we permit the SourceReader and SinkWriter to load hardware based transforms
        private bool DEFAULT_ALLOW_HARDWARE_TRANSFORMS = false;

        // This is the sink writer that creates the copy of the output file
        protected IMFSinkWriter sinkWriter;

        // This is the source reader the reads the contents of the input file
        protected IMFSourceReader sourceReader;

        // this is used to configure the input and output
        private IMFMediaType sourceReaderNativeAudioMediaType = null;
        private bool disposedValue;

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Constructor
        /// </summary>
        /// <history>
        ///    01 Nov 18  Cynic - Started
        /// </history>
        public AudioFileCopier()
        {
            // we always have to initialize MF. The 0x00020070 here is the WMF version 
            // number used by the MF.Net samples. Not entirely sure if it is appropriate
            HResult hr = MFExtern.MFStartup(0x00020070, MFStartup.Full);
            if (hr != 0)
            {
                throw new Exception("Constructor: call to MFExtern.MFStartup returned " + hr.ToString());
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                }

                CloseAllMediaDevices();

                // Shut down MF
                MFExtern.MFShutdown();
                disposedValue = true;
            }
        }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }



        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// A centralized place to close down all media devices.
        /// </summary>
        /// <history>
        ///    01 Nov 18  Cynic - Started
        /// </history>
        private void CloseAllMediaDevices()
        {
            // Shut down the source reader
            if (sourceReader != null)
            {
                Marshal.ReleaseComObject(sourceReader);
                sourceReader = null;
            }

            // close the sink writer
            if (sinkWriter != null)
            {
                // note we could Finalize_() this here but there
                // is no need. That is done when the stream ends
                Marshal.ReleaseComObject(sinkWriter);
                sinkWriter = null;
            }

            if (sourceReaderNativeAudioMediaType != null)
            {
                Marshal.ReleaseComObject(sourceReaderNativeAudioMediaType);
                sourceReaderNativeAudioMediaType = null;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Starts/Stops the file copy
        /// 
        /// Because this code is intended for demo purposes, and in the interests of
        /// reducing complexity, it is extremely linear, step-by-step and kicked off
        /// directly from a button press in the main form. Doubtless there is much 
        /// refactoring that could be done.
        /// 
        /// </summary>
        /// <history>
        ///    01 Nov 18  Cynic - Started
        /// </history>
        public void Start(string sourceFilename, string outputFilename)
        {

            // Windows 10, by default, provides an adequate set of codecs which the Sink Writer can
            // find to write out the MP3 file. This is not true on Windows 7.

            // at this time is has not been possible to figure out how to load a set of codecs
            // that will work. So a warning will be issued. If you get this working feel
            // free to send though the update. See the MFTRegisterLocalByCLSID call in the 
            // TantaCaptureToFileViaReaderWriter app for local MFT registration details
            OperatingSystem os = Environment.OSVersion;
            int versionID = ((os.Version.Major * 10) + os.Version.Minor);
            if (versionID < 62)
            {
                throw new Exception("You appear to be on a version of Windows less than 10. Earlier versions of Windows do not provide a default set of codecs to support this option.\n\nThis operation may not work.");
            }

            try
            {

                // check our source filename is correct and usable
                if ((sourceFilename == null) || (sourceFilename.Length == 0))
                {
                    throw new Exception("No Source Filename and path. Cannot continue.");
                }
                // check our output filename is correct and usable
                if ((outputFilename == null) || (outputFilename.Length == 0))
                {
                    throw new Exception("No Output Filename and path. Cannot continue.");
                }
                // check the path is rooted
                if (Path.IsPathRooted(sourceFilename) == false)
                {
                    throw new Exception("No Source Filename and path is not rooted. A full directory and path is required. Cannot continue.");
                }
                if (Path.IsPathRooted(outputFilename) == false)
                {
                    throw new Exception("No Output Filename and path is not rooted. A full directory and path is required. Cannot continue.");
                }

                // Set up a source reader and sink writer and copy the file
                CopyFile(sourceFilename, outputFilename);
            }
            catch
            {
                throw;
            }
        }

        /// +=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=+=
        /// <summary>
        /// Does everything to copy a file. Opens the Source Reader and Sink Writer
        /// configures the streams and then, because a Synchronous version
        /// of the Source Reader is used, we sit in a loop and perform the copy.
        /// 
        /// Any errors here simply throw an exception and must be trapped elsewhere
        /// 
        /// Note that because this code is intended for demo purposes, it has been
        /// kept very simple and linear. Most of the things that could have been 
        /// refactored in a common procedure are simply 
        /// written out in order to make it obvious what is going on.
        /// </summary>
        /// <param name="sourceFileName">the source file name</param>
        /// <param name="outputFileName">the name of the output file</param>
        /// <history>
        ///    01 Nov 18  Cynic - Originally Written
        /// </history>
        public void CopyFile(string sourceFileName, string outputFileName)
        {
            HResult hr;

            int sinkWriterOutputAudioStreamId = -1;
            int audioSamplesProcessed = 0;
            bool audioStreamIsAtEOS = false;
            int sourceReaderAudioStreamId = -1;

            // not keen on endless loops. This is the maximum number
            // of streams we will check in the source reader.
            const int MAX_SOURCEREADER_STREAMS = 100;

            // create the SourceReader
            sourceReader = TantaWMFUtils.CreateSourceReaderSyncFromFile(sourceFileName, DEFAULT_ALLOW_HARDWARE_TRANSFORMS);
            if (sourceReader == null)
            {
                // we failed
                throw new Exception("CopyFile: Failed to create SourceReader, Nothing will work.");
            }
            // create the SinkWriter
            sinkWriter = TantaWMFUtils.CreateSinkWriterFromFile(outputFileName, DEFAULT_ALLOW_HARDWARE_TRANSFORMS);
            if (sinkWriter == null)
            {
                // we failed
                throw new Exception("CopyFile: Failed to create Sink Writer, Nothing will work.");
            }

            // find the first audio stream and identify the default Media Type
            // it is using. We could look into the stream and enumerate all of the 
            // types on offer and choose one from the list - but for a copy operation 
            // the default will be quite suitable.

            sourceReaderNativeAudioMediaType = null;
            for (int streamIndex = 0; streamIndex < MAX_SOURCEREADER_STREAMS; streamIndex++)
            {
                IMFMediaType workingType = null;
                Guid guidMajorType = Guid.Empty;

                // the the major media type - we are looking for audio
                hr = sourceReader.GetNativeMediaType(streamIndex, 0, out workingType);
                if (hr == HResult.MF_E_NO_MORE_TYPES) break;
                if (hr == HResult.MF_E_INVALIDSTREAMNUMBER) break;
                if (hr != HResult.S_OK)
                {
                    // we failed
                    throw new Exception("CopyFile: failed on call to GetNativeMediaType, retVal=" + hr.ToString());
                }
                if (workingType == null)
                {
                    // we failed
                    throw new Exception("CopyFile: failed on call to GetNativeMediaType, workingType == null");
                }

                // what major type does this stream have?
                hr = workingType.GetMajorType(out guidMajorType);
                if (hr != HResult.S_OK)
                {
                    throw new Exception("CopyFile:  call to workingType.GetMajorType failed. Err=" + hr.ToString());
                }
                if (guidMajorType == null)
                {
                    throw new Exception("CopyFile:  call to workingType.GetMajorType failed. guidMajorType == null");
                }

                // test for audio (there can be others)
                if ((guidMajorType == MFMediaType.Audio))
                {
                    // this stream represents a audio type
                    sourceReaderNativeAudioMediaType = workingType;
                    sourceReaderAudioStreamId = streamIndex;
                    // the sourceReaderNativeAudioMediaType will be released elsewhere
                    break;
                }

                // if we get here release the type - we do not use it
                if (workingType != null)
                {
                    Marshal.ReleaseComObject(workingType);
                    workingType = null;
                }
            }

            // at this point we expect we can have a native video or a native audio media type
            // or both, but not neither. if we don't we cannot carry on            
            if (sourceReaderNativeAudioMediaType == null)
            {
                // we failed
                throw new Exception("CopyFile: failed on call to GetNativeMediaType, sourceReaderNativeAudioMediaType == null");
            }

            // set the media type on the reader - this is the media type the source reader will output
            // this does not have to match the media type in the file. If it does not the Source Reader
            // will attempt to load a transform to perform the conversion. In this case we know it 
            // matches because the type we are using IS the same media type we got from the stram
            hr = sourceReader.SetCurrentMediaType(sourceReaderAudioStreamId, null, sourceReaderNativeAudioMediaType);
            if (hr != HResult.S_OK)
            {
                // we failed
                throw new Exception("CopyFile: failed on call to SetCurrentMediaType(a), retVal=" + hr.ToString());
            }

            // add a stream to the sink writer. The mediaType specifies the format of the samples that will be written 
            // to the file. Note that it does not necessarily need to match the format of the samples
            // we provide to the sink writer. In this case, because we are copying a file, the media type
            // we write to disk IS the media type the source reader reads from the disk.
            hr = sinkWriter.AddStream(sourceReaderNativeAudioMediaType, out sinkWriterOutputAudioStreamId);
            if (hr != HResult.S_OK)
            {
                // we failed
                throw new Exception("CopyFile: Failed adding the output stream(a), retVal=" + hr.ToString());
            }

            // Set the input format for a stream on the sink writer. Note the use of the stream index here
            // The input format does not have to match the output format that is written to the media sink
            // If the formats do not match, this call attempts to load an transform that can convert from 
            // the input format to the target format. If it cannot find one, and this is not a sure thing, 
            // it will throw an exception.
            hr = sinkWriter.SetInputMediaType(sinkWriterOutputAudioStreamId, sourceReaderNativeAudioMediaType, null);
            if (hr != HResult.S_OK)
            {
                // we failed
                throw new Exception("CopyFile: Failed on calling SetInputMediaType(a) on the writer, retVal=" + hr.ToString());
            }

            // begin writing on the sink writer
            hr = sinkWriter.BeginWriting();
            if (hr != HResult.S_OK)
            {
                // we failed
                throw new Exception("CopyFile: failed on call to BeginWriting, retVal=" + hr.ToString());
            }

            // we sit in a loop here and get the sample from the source reader and write it out
            // to the sink writer. An EOS (end of sample) value in the flags will signal the end.
            // Note the application will appear to be locked up while we are in here. We are ok
            // with this because it is quick and we want to keep things simple
            while (true)
            {
                int actualStreamIndex;
                MF_SOURCE_READER_FLAG actualStreamFlags;
                long timeStamp = 0;
                IMFSample workingMediaSample = null;

                // Request the next sample from the media source. Note that this could be
                // any type of media sample (video, audio, subtitles etc). We do not know
                // until we look at the stream ID. We saved the stream ID earlier when
                // we obtained the media types and so we can branch based on that. 

                // In reality since we only set up one stream (audio) this will always be
                // the audio stream - but there is no need to assume this and the
                // TantaVideoFileCopyViaReaderWriter demonstrates an example with two 
                // streams (audio and video)
                hr = sourceReader.ReadSample(
                    TantaWMFUtils.MF_SOURCE_READER_ANY_STREAM,
                    0,
                    out actualStreamIndex,
                    out actualStreamFlags,
                    out timeStamp,
                    out workingMediaSample
                    );
                if (hr != HResult.S_OK)
                {
                    // we failed
                    throw new Exception("CopyFile: Failed on calling the ReadSample on the reader, retVal=" + hr.ToString());
                }

                // the sample may be null if either end of stream or a stream tick is returned
                if (workingMediaSample == null)
                {
                    // just ignore, the flags will have the information we need.
                }
                else
                {
                    // the sample is not null
                    if (actualStreamIndex == sourceReaderAudioStreamId)
                    {
                        // audio data
                        // ensure discontinuity is set for the first sample in each stream
                        if (audioSamplesProcessed == 0)
                        {
                            // audio data
                            hr = workingMediaSample.SetUINT32(MFAttributesClsid.MFSampleExtension_Discontinuity, 1);
                            if (hr != HResult.S_OK)
                            {
                                // we failed
                                throw new Exception("CopyFile: Failed on calling SetUINT32 on the sample, retVal=" + hr.ToString());
                            }
                            // remember this - we only do it once
                            audioSamplesProcessed++;
                        }
                        hr = sinkWriter.WriteSample(sinkWriterOutputAudioStreamId, workingMediaSample);
                        if (hr != HResult.S_OK)
                        {
                            // we failed
                            throw new Exception("CopyFile: Failed on calling the WriteSample on the writer, retVal=" + hr.ToString());
                        }
                    }

                    // release the sample
                    if (workingMediaSample != null)
                    {
                        Marshal.ReleaseComObject(workingMediaSample);
                        workingMediaSample = null;
                    }
                }

                // do we have a stream tick event?
                if ((actualStreamFlags & MF_SOURCE_READER_FLAG.StreamTick) != 0)
                {
                    if (actualStreamIndex == sourceReaderAudioStreamId)
                    {
                        // audio stream
                        hr = sinkWriter.SendStreamTick(sinkWriterOutputAudioStreamId, timeStamp);
                    }
                    else
                    {
                    }
                }

                // is this stream at an END of Segment
                if ((actualStreamFlags & MF_SOURCE_READER_FLAG.EndOfStream) != 0)
                {
                    // We have an EOS - but is it on the audio channel?
                    if (actualStreamIndex == sourceReaderAudioStreamId)
                    {
                        // audio stream
                        // have we seen this before?
                        if (audioStreamIsAtEOS == false)
                        {
                            hr = sinkWriter.NotifyEndOfSegment(sinkWriterOutputAudioStreamId);
                            if (hr != HResult.S_OK)
                            {
                                // we failed
                                throw new Exception("CopyFile: Failed on calling the NotifyEndOfSegment on audio stream, retVal=" + hr.ToString());
                            }
                            audioStreamIsAtEOS = true;
                        }
                        // audio stream
                    }
                    else
                    {
                    }

                    // our exit condition depends on which streams are in use
                    if (sourceReaderNativeAudioMediaType != null)
                    {
                        // only audio is active, if the audio stream is EOS we can leave
                        if (audioStreamIsAtEOS == true) break;
                    }
                }
            } // bottom of endless for loop

            hr = sinkWriter.Finalize_();
            if (hr != HResult.S_OK)
            {
                // we failed
                throw new Exception("Failed on call tosinkWriter.Finalize(), retVal=" + hr.ToString());
            }

        }
    }
}
