﻿using System;

using FreeTrainSimulator.Common.Input;
using FreeTrainSimulator.Graphics.MapView;
using FreeTrainSimulator.Models.Simplified;
using FreeTrainSimulator.Models.Track;

using Microsoft.Xna.Framework;

using Orts.Formats.Msts.Files;

namespace FreeTrainSimulator.Dispatcher
{
    public class PathEditorChangedEventArgs : EventArgs
    {
        public TrainPathBase Path { get; }

        public PathEditorChangedEventArgs(TrainPathBase path)
        {
            Path = path;
        }
    }

    internal sealed class PathEditor : PathEditorBase
    {
        private Path path;
        private long lastPathClickTick;
        private bool validPointAdded;

        public string FilePath => path?.FilePath;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathChanged;

        internal event EventHandler<PathEditorChangedEventArgs> OnPathUpdated;

        public PathEditor(ContentArea contentArea) : base(contentArea) { }

        public bool InitializePath(Path path)
        {
            try
            {
                if (path != null)
                {
                    this.path = path;
                    PathFile patFile = new PathFile(path.FilePath);
                    InitializePath(patFile, path.FilePath);
                }
                else
                {
                    InitializePath(null, null);
                }
                OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
                return true;
            }
            catch (Exception ex) when (ex is Exception)
            {
                return false;
            }
        }

        public void InitializeNewPath()
        {
            InitializePath();
            OnPathChanged?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }

        public void MouseAction(Point screenLocation, KeyModifiers keyModifiers)
        {
            if (Environment.TickCount64 - lastPathClickTick < 500 && validPointAdded) //considered as double click
            {
                _ = AddPathEndPoint();
            }
            else
            {
                validPointAdded = AddPathPoint();
            }
            lastPathClickTick = Environment.TickCount64;
            OnPathUpdated?.Invoke(this, new PathEditorChangedEventArgs(TrainPath));
        }
    }
}
