using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
using AdiIRCAPIv2.Interfaces;

namespace AdiIRC_LibreTranslate_plugin
{
    // Reads Elite Dangerous log files and processes chat messages
    // Monitors the log directory for new files and file changes using FileSystemWatcher
    // Fires events when new log files are detected or chat messages are received
    internal class EliteDangerousLogReader : IDisposable
    {
        private readonly string _logDirectoryPath;
        private FileSystemWatcher _fileWatcher;
        private StreamReader _currentFileReader;
        private string _currentLogFilePath;
        private long _currentPosition = 0;
        private readonly JavaScriptSerializer _jsonSerializer;
        private bool _isMonitoring = false;
        private readonly object _lockObject = new object();
        private readonly IPluginHost _host;
        
        // The latest log file name
        public string LatestLogFileName { get; private set; }
        
        // Events that fire when log file events occur
        public event EventHandler<string> NewLogFileDetected;
        public event EventHandler<ChatMessageEventArgs> ChatMessageReceived;

        public EliteDangerousLogReader(string logDirectoryPath, IPluginHost host = null, bool enableDebugLogging = false)
        {
            // Replace environment variables if they exist in the path
            _logDirectoryPath = Environment.ExpandEnvironmentVariables(logDirectoryPath);
            _jsonSerializer = new JavaScriptSerializer();
            _host = host;
            
            // Initialize the file watcher
            InitializeFileWatcher();
        }

        // Initialize the FileSystemWatcher to monitor the log directory
        private void InitializeFileWatcher()
        {
            if (!Directory.Exists(_logDirectoryPath))
            {
                LogMessage($"Log directory not found: {_logDirectoryPath}");
                return;
            }

            _fileWatcher = new FileSystemWatcher(_logDirectoryPath)
            {
                Filter = "Journal.*.log",
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size | NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = false
            };
            
            _fileWatcher.Changed += OnFileChanged;
            _fileWatcher.Created += OnFileCreated;
            _fileWatcher.Error += OnFileWatcherError;
        }

        // Starts monitoring for new log files and log entries
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
            
            // Start the file watcher
            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = true;
            }
            
            _isMonitoring = true;
        }

        // Stops monitoring for new log files
        public void StopMonitoring()
        {
            if (!_isMonitoring)
                return;

            if (_fileWatcher != null)
                _fileWatcher.EnableRaisingEvents = false;
                
            CloseCurrentLogFile();
            _isMonitoring = false;
        }

        // Handle file watcher errors
        private void OnFileWatcherError(object sender, ErrorEventArgs e)
        {
            LogMessage($"File watcher error: {e.GetException().Message}");
            
            // Try to restart the watcher
            if (_fileWatcher != null && _isMonitoring)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.EnableRaisingEvents = true;
            }
        }

        // Called when a file in the watched directory is changed
        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            lock (_lockObject)
            {
                // Handle duplicate events by checking if this is our current file
                if (e.FullPath == _currentLogFilePath)
                {
                    // The current log file has been updated, read new content
                    ReadNewLogFileContent();
                }
            }
        }

        // Called when a new file is created in the watched directory
        private void OnFileCreated(object sender, FileSystemEventArgs e)
        {
            lock (_lockObject)
            {
                if (Path.GetFileName(e.FullPath).StartsWith("Journal.") && e.FullPath.EndsWith(".log"))
                {
                    // Check if this new file is newer than our current one
                    DateTime newFileTime = File.GetLastWriteTime(e.FullPath);
                    DateTime currentFileTime = string.IsNullOrEmpty(_currentLogFilePath) ? 
                        DateTime.MinValue : File.GetLastWriteTime(_currentLogFilePath);
                        
                    if (newFileTime > currentFileTime)
                    {
                        // This is a newer log file, switch to it
                        FindLatestLogFile();
                    }
                }
            }
        }

        // Finds the latest log file in the specified directory and updates the LatestLogFileName property
        private void FindLatestLogFile()
        {
            lock (_lockObject)
            {
                try
                {
                    if (!Directory.Exists(_logDirectoryPath))
                    {
                        return;
                    }

                    // Get all log files matching the pattern
                    string[] logFiles = Directory.GetFiles(_logDirectoryPath, "Journal.*.log");

                    if (logFiles.Length == 0)
                    {
                        return;
                    }

                    // Find the file with the most recent write time
                    string latestFile = logFiles
                        .OrderByDescending(f => File.GetLastWriteTime(f))
                        .FirstOrDefault();

                    if (latestFile == null)
                    {
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
                    LogMessage($"Error finding latest log file: {ex.Message}");
                }
            }
        }
        
        // Opens the current log file for reading and sets up position tracking
        private void OpenAndWatchCurrentLogFile()
        {
            try
            {
                CloseCurrentLogFile();
                
                if (string.IsNullOrEmpty(_currentLogFilePath) || !File.Exists(_currentLogFilePath))
                {
                    return;
                }
                
                // Open the file for reading with read sharing enabled
                var fileStream = new FileStream(
                    _currentLogFilePath, 
                    FileMode.Open, 
                    FileAccess.Read, 
                    FileShare.ReadWrite);
                
                _currentFileReader = new StreamReader(fileStream, Encoding.UTF8);
                
                // Process all existing entries in the file
                _currentPosition = 0;
                ReadNewLogFileContent(true);
            }
            catch (Exception ex)
            {
                LogMessage($"Error opening log file: {ex.Message}");
            }
        }
        
        // Closes the current log file reader if open
        private void CloseCurrentLogFile()
        {
            if (_currentFileReader != null)
            {
                _currentFileReader.Dispose();
                _currentFileReader = null;
            }
        }

        // Reads any new content in the current log file
        private void ReadNewLogFileContent(bool processAllEntries = false)
        {
            try
            {
                if (_currentFileReader == null || _currentFileReader.BaseStream == null)
                {
                    return;
                }

                lock (_lockObject)
                {
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
                    else
                    {
                        // Start from the beginning for a full read
                        _currentFileReader.BaseStream.Seek(0, SeekOrigin.Begin);
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
            }
            catch (Exception ex)
            {
                LogMessage($"Error reading log file content: {ex.Message}");
            }
        }
        
        // Process a log entry and handle relevant events
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
                LogMessage($"Error processing log entry: {ex.Message}");
            }
        }
        
        // Logs a message to the AdiIRC window if host is available
        private void LogMessage(string message)
        {
            if (_host != null)
            {
                _host.ActiveIWindow.OutputText($"[ED Log] {message}");
            }
        }
        
        // Manually triggers a check for the latest log file
        public void CheckNow()
        {
            ThreadPool.QueueUserWorkItem(_ => FindLatestLogFile());
        }

        // Gets the full path to the latest log file
        public string GetLatestLogFilePath()
        {
            if (string.IsNullOrEmpty(LatestLogFileName))
                return null;
                
            return Path.Combine(_logDirectoryPath, LatestLogFileName);
        }

        public void Dispose()
        {
            StopMonitoring();
            
            if (_fileWatcher != null)
            {
                _fileWatcher.Changed -= OnFileChanged;
                _fileWatcher.Created -= OnFileCreated;
                _fileWatcher.Error -= OnFileWatcherError;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }
            
            CloseCurrentLogFile();
        }
    }
    
    // Event args class for chat message events
    public class ChatMessageEventArgs : EventArgs
    {
        public string Channel { get; set; }
        public string From { get; set; }
        public string Message { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
