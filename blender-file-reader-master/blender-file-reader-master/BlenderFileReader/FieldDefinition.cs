﻿using System;
using System.Linq;

namespace BlenderFileReader
{
    /// <summary>
    /// A field of a structure as defined by SDNA.
    /// </summary>
    public struct FieldDefinition
    {
        /// <summary>
        /// Name of the field.
        /// </summary>
        public readonly string Name;
        /// <summary>
        /// Type (as defined by SDNA) of the field.
        /// </summary>
        public readonly TypeDefinition Type;

        /// <summary>
        /// Shortcut to Type.IsPrimitive; indicates if the field is a structure.
        /// </summary>
        public bool IsPrimitive { get { return Type.IsPrimitive; } }

        /// <summary>
        /// Indicates if the field is a pointer.
        /// </summary>
        public bool IsPointer { get { return Name[0] == '*'; } }

        /// <summary>
        /// Indicates if the field is an array.
        /// </summary>
        public bool IsArray { get { return Name.Contains('['); } }

        /// <summary>
        /// Gets the underlying StructureDefinition if the field is not a primitive.
        /// </summary>
        public StructureDefinition Structure { get { if(IsPrimitive) throw new InvalidOperationException(Name + " is a primitive and does not have an SDNAStructure."); if(structure == null) InitializeStructure(); return structure.Value; } }
        private StructureDefinition? structure;
        private bool isInitialized;

        private StructureDNA sdna;

        /// <summary>
        /// Creates a new field.
        /// </summary>
        /// <param name="name">Name of the field.</param>
        /// <param name="type">Type (as defined by SDNA) of the field.</param>
        /// <param name="sdna">Structure DNA for the file.</param>
        public FieldDefinition(string name, TypeDefinition type, StructureDNA sdna)
        {
            Name = name;
            Type = type;
            structure = null;
            isInitialized = false;
            this.sdna = sdna;
        }

        /// <summary>
        /// Creates a new field.
        /// </summary>
        /// <param name="nameIndex">Index of SDNA.NameList containing the name of the field.</param>
        /// <param name="typeIndex">Index of SDNA.TypeList containing the type of the field.</param>
        /// <param name="sdna">Structure DNA in which the field is contained.</param>
        public FieldDefinition(short nameIndex, short typeIndex, StructureDNA sdna)
        {
            Name = sdna.NameList[nameIndex];
            Type = sdna.TypeList[typeIndex];
            this.sdna = sdna;

            if(Type.Name.Count(v => { return v == '['; }) > 2)
                throw new Exception("A 3D array is present and this program is not set up to handle that.");

            isInitialized = false;
            structure = null;
        }

        /// <summary>
        /// If the field is non-primitive, this will populate Structure. Safe to call on primitives.
        /// </summary>
        public void InitializeStructure()
        {
            if(isInitialized)
                throw new InvalidOperationException("Can't initialize a field's structure twice.");
            if(IsPrimitive)
                return;
            isInitialized = true;

            string name = Type.Name; // can't use 'this'
            structure = sdna.StructureList.Find(v => { return v.StructureTypeName == name; });
            structure.Value.InitializeFields();
        }
    }
}
