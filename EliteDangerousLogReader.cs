using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Timers;
using System.Web.Script.Serialization;

namespace AdiIRC_LibreTranslate_plugin
{
    /** This hot garbage is an mostly AI generated C# class that reads Elite Dangerous log files
     * Preferably only dig into it using AI since it's way too messy and long to read manually
     * Probably needs a lot of refactoring and cleanup, but we vibecoding now.
     * 
     * Reads Elite Dangerous log files and processes chat messages
     * Fires events when new log files are detected or chat messages are received
     */
    internal class EliteDangerousLogReader : IDisposable
    {
        private readonly string _logDirectoryPath;
        private System.Timers.Timer _checkTimer;
        private bool _isMonitoring = false;
        private FileSystemWatcher _fileWatcher;
        private StreamReader _currentFileReader;
        private string _currentLogFilePath;
        private long _currentPosition = 0;
        private readonly JavaScriptSerializer _jsonSerializer;
        
        // The latest log file name
        public string LatestLogFileName { get; private set; }
        
        // Events that fire when log file events occur
        public event EventHandler<string> NewLogFileDetected;
        public event EventHandler<ChatMessageEventArgs> ChatMessageReceived;

        public EliteDangerousLogReader(string logDirectoryPath)
        {
            // Replace environment variables if they exist in the path
            _logDirectoryPath = Environment.ExpandEnvironmentVariables(logDirectoryPath);
            _jsonSerializer = new JavaScriptSerializer();

            // Initialize the timer but don't start it yet
            _checkTimer = new System.Timers.Timer(5000);
            _checkTimer.Elapsed += CheckForNewLogFiles;
            _checkTimer.AutoReset = true;
            
            // Initialize the file watcher but don't start it yet
            _fileWatcher = new FileSystemWatcher(_logDirectoryPath)
            {
                Filter = "Journal.*.log",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = false
            };
            
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileCreated;
        }

        /// <summary>
        /// Starts monitoring for new log files and log entries
        /// </summary>
        public void StartMonitoring()
        {
            if (_isMonitoring)
                return;

            // Find the latest log file immediately
            FindLatestLogFile();
            
            if (!string.IsNullOrEmpty(LatestLogFileName))
            {
                // Start watching the latest file
                OpenAndWatchCurrentLogFile();
            }
            
            // Start the timer to check periodically for new log files
            _checkTimer.Start();
            
            // Start the file watcher
            _fileWatcher.EnableRaisingEvents = true;
            
            _isMonitoring = true;
        }

        /// <summary>
        /// Stops monitoring for new log files
        /// </summary>
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            _checkTimer.Stop();
            _fileWatcher.EnableRaisingEvents = false;
            CloseCurrentLogFile();
            _isMonitoring = false;
        }

        /// <summary>
        /// Gets called by the timer to check for new log files
        /// </summary>
        private void CheckForNewLogFiles(object sender, ElapsedEventArgs e)
        {
            FindLatestLogFile();
        }

        /// <summary>
        /// Called when a file in the watched directory is changed
        /// </summary>
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath == _currentLogFilePath)
            {
                // The current log file has been updated, read new content
                ReadNewLogFileContent();
            }
        }

        /// <summary>
        /// Called when a new file is created in the watched directory
        /// </summary>
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            if (Path.GetFileName(e.FullPath).StartsWith("Journal.") && e.FullPath.EndsWith(".log"))
            {
                // A new log file was created, check if it's newer than our current one
                FindLatestLogFile();
            }
        }

        /// <summary>
        /// Finds the latest log file in the specified directory and updates the LatestLogFileName property
        /// </summary>
        private void FindLatestLogFile()
        {
            try
            {
                if (!Directory.Exists(_logDirectoryPath))
                {
                    // Directory doesn't exist, can't find log files
                    return;
                }

                // Get all log files matching the pattern
                string[] logFiles = Directory.GetFiles(_logDirectoryPath, "Journal.*.log");

                if (logFiles.Length == 0)
                {
                    // No log files found
                    return;
                }

                // Find the file with the most recent write time
                string latestFile = logFiles
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .FirstOrDefault();

                if (latestFile == null)
                {
                    // No files found after filtering
                    return;
                }

                // Extract just the filename without the path
                string latestFileName = Path.GetFileName(latestFile);

                // If this is a different file than what we had before
                if (LatestLogFileName != latestFileName)
                {
                    string previousLogFile = LatestLogFileName;
                    LatestLogFileName = latestFileName;
                    _currentLogFilePath = latestFile;
                    
                    // If we were already monitoring, switch to the new file
                    if (_isMonitoring)
                    {
                        CloseCurrentLogFile();
                        OpenAndWatchCurrentLogFile();
                    }
                    
                    // Raise the event to notify listeners
                    NewLogFileDetected?.Invoke(this, LatestLogFileName);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error finding latest log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Opens the current log file for reading and sets up position tracking
        /// </summary>
        private void OpenAndWatchCurrentLogFile()
        {
            try
            {
                CloseCurrentLogFile();
                
                if (string.IsNullOrEmpty(_currentLogFilePath) || !File.Exists(_currentLogFilePath))
                {
                    return;
                }
                
                // Open the file for reading
                var fileStream = new FileStream(
                    _currentLogFilePath, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.ReadWrite);
                
                _currentFileReader = new StreamReader(fileStream, Encoding.UTF8);
                
                // Go to the end of existing content if file already has content
                _currentPosition = fileStream.Length;
                
                // If the file has content, seek to the beginning and process all existing entries
                if (_currentPosition > 0)
                {
                    _currentFileReader.BaseStream.Seek(0, SeekOrigin.Begin);
                    ReadNewLogFileContent(true);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening log file: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Closes the current log file reader if open
        /// </summary>
        private void CloseCurrentLogFile()
        {
            if (_currentFileReader != null)
            {
                _currentFileReader.Dispose();
                _currentFileReader = null;
            }
        }

        /// <summary>
        /// Reads any new content in the current log file
        /// </summary>
        private void ReadNewLogFileContent(bool processAllEntries = false)
        {
            try
            {
                if (_currentFileReader == null || _currentFileReader.BaseStream == null)
                {
                    return;
                }

                // If we should read from the current position (not process all entries)
                if (!processAllEntries)
                {
                    // Check if there is new content by comparing file length to current position
                    _currentFileReader.BaseStream.Seek(0, SeekOrigin.End);
                    long newLength = _currentFileReader.BaseStream.Position;
                    
                    if (newLength <= _currentPosition)
                    {
                        return; // No new content
                    }
                    
                    // Move to where we last read
                    _currentFileReader.BaseStream.Seek(_currentPosition, SeekOrigin.Begin);
                }

                // Read and process all lines until end of file
                string line;
                while ((line = _currentFileReader.ReadLine()) != null)
                {
                    ProcessLogEntry(line);
                }
                
                // Update current position
                _currentPosition = _currentFileReader.BaseStream.Position;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading log file content: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Process a log entry and handle relevant events
        /// </summary>
        private void ProcessLogEntry(string jsonLine)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(jsonLine))
                    return;
                
                // Parse JSON
                var logEntry = _jsonSerializer.Deserialize<Dictionary<string, object>>(jsonLine);
                
                if (logEntry == null)
                    return;
                
                // Check for "ReceiveText" events
                if (logEntry.TryGetValue("event", out object eventType) && 
                    eventType.ToString() == "ReceiveText")
                {
                    // Extract message channel and text
                    if (logEntry.TryGetValue("Channel", out object channelObj) && 
                        logEntry.TryGetValue("From", out object fromObj) && 
                        logEntry.TryGetValue("Message", out object messageObj))
                    {
                        string channel = channelObj.ToString().ToLower();
                        string from = fromObj.ToString();
                        string message = messageObj.ToString();
                        
                        // Check if channel is one we're interested in (wing, local, friend, player)
                        if (channel == "wing" || channel == "local" || 
                            channel == "friend" || channel == "player")
                        {
                            // Fire event with relevant info
                            var chatMessage = new ChatMessageEventArgs
                            {
                                Channel = channel,
                                From = from,
                                Message = message,
                                Timestamp = DateTime.UtcNow
                            };
                            
                            ChatMessageReceived?.Invoke(this, chatMessage);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing log entry: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Manually triggers a check for the latest log file
        /// </summary>
        public void CheckNow()
        {
            FindLatestLogFile();
        }

        /// <summary>
        /// Gets the full path to the latest log file
        /// </summary>
        public string GetLatestLogFilePath()
        {
            if (string.IsNullOrEmpty(LatestLogFileName))
                return null;
                
            return Path.Combine(_logDirectoryPath, LatestLogFileName);
        }

        public void Dispose()
        {
            StopMonitoring();
            
            if (_checkTimer != null)
            {
                _checkTimer.Elapsed -= CheckForNewLogFiles;
                _checkTimer.Dispose();
                _checkTimer = null;
            }
            
            if (_fileWatcher != null)
            {
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Created -= OnFileCreated;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
            
            CloseCurrentLogFile();
        }
    }
    
    /// <summary>
    /// Event args class for chat message events
    /// </summary>
    public class ChatMessageEventArgs : EventArgs
    {
        public string Channel { get; set; }
        public string From { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
