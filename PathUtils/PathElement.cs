﻿using System;

namespace EPubLibrary.PathUtils
{
    public enum PathType
    {
        Root = 0,
        Folder = 1,
        File = 2,
    };

    public class PathElement : ICloneable
    {
        private readonly PathType _pathType = PathType.File;
        private readonly string _name = string.Empty;

        public PathElement(string name, PathType type)
        {
            if (string.IsNullOrEmpty(name) && type != PathType.Root)
            {
                throw new ArgumentException("Can't have empty name for non root path element");
            }
            _pathType = type;
            _name = name;
        }


        public PathType Type
        {
            get { return _pathType; }
        }



        public string Name
        {
            get { return _name; }
        }

        public object Clone()
        {
            return MemberwiseClone();
        }
    }
}
