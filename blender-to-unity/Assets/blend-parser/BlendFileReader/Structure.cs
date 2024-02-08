﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

using BlenderToUnity;

namespace BlenderFileReader
{
    /// <summary>
    /// Represents a SDNA structure filled with data.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")] // nq means "no quotes", without it the value returned by DebuggerDisplay will be quoted.
    public class Structure : IStructField, IEnumerable<IField>
    {
        // this string is hidden from the debugger, but used as the display string for the field instead of ToString().
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay { get { return string.Format("{0}: [{1} field{2}]", Name == TypeName ? TypeName : "\"" + Name + "\" (type " + TypeName + ")", NumFields, NumFields == 1 ? "" : "s"); } }

        /// <summary>
        /// Parses a file block. If the file block is SDNA or another block with block.count == 0, it will return null.
        /// </summary>
        /// <param name="block">The block to parse.</param>
        /// <param name="blocksParsed">Number of blocks parsed so far.</param>
        /// <param name="file">Source file for the structures.</param>
        /// <returns>An array of PopulatedStructures, or { null } if no structures are defined.</returns>
        public static Structure[] ParseFileBlock(FileBlock block, int blocksParsed, BlenderFile file)
        {
            var blockType = file.StructureDNA.TypeList[block.SDNAIndex];
            f.print($"Block {block.Index}:{block.Code} sdna: {block.SDNAIndex} ({blockType})");

            if(block.Count == 0 || block.Code == "DNA1")
                return null;

            StructureDNA sdna = file.StructureDNA;

            if(block.Data.Length != sdna.StructureList[block.SDNAIndex].StructureTypeSize * block.Count)
            {
                // generally, these are things like raw data; packed files, preview images, and arrays of pointers that are themselves pointed to.
                // I have no idea what TEST and REND do.
                file.RawBlockMessages.Add(blocksParsed + " " + block.OldMemoryAddress.ToString("X" + (file.PointerSize * 2)) + " " + block.Code + " " + block.SDNAIndex + " " + sdna.StructureList[block.SDNAIndex].StructureTypeSize * block.Count + " " + block.Data.Length);
                return null;
            }

            Structure[] output = new Structure[block.Count];

            if(block.Count > 1)
                for(int i = 0; i < block.Count; i++)
                {
                    byte[] data = new byte[block.Size / block.Count];
                    for(int j = 0; j < block.Size / block.Count; j++)
                        data[j] = block.Data[i * (block.Size / block.Count) + j];
                    output[i] = new Structure(data, sdna.StructureList[block.SDNAIndex], file);
                    output[i].Size = sdna.StructureList[block.SDNAIndex].StructureTypeSize;
                    output[i].TypeName = sdna.StructureList[block.SDNAIndex].StructureTypeName;
                    output[i].ContainingBlock = block;
                }
            else
            {
                output[0] = new Structure(block.Data, sdna.StructureList[block.SDNAIndex], file);
                output[0].Size = sdna.StructureList[block.SDNAIndex].StructureTypeSize;
                output[0].TypeName = sdna.StructureList[block.SDNAIndex].StructureTypeName;
                output[0].ContainingBlock = block;
            }

            return output;
        }

        /// <summary>
        /// The FileBlock containing this structure. May be null for embedded structures.
        /// </summary>
        public FileBlock ContainingBlock { get; private set; }

        /// <summary>
        /// The BlenderFile containing this structure.
        /// </summary>
        public BlenderFile ContainingFile { get; private set; }

        /// <summary>
        /// A count of the number of fields in this structure and all contained structures.
        /// </summary>
        public int NumFields
        {
            get
            {
                Structure temp;
                int total = 0;
                foreach(IField f in this)
                    if((temp = f as Structure) != null)
                        total += temp.NumFields;
                    else
                        total++;
                return total;
            }
        }

        /// <summary>
        /// Value contained in the field. Use this sparingly.
        /// </summary>
        dynamic IField.Value { get { return this; } }

        /// <summary>
        /// A list of fields contained in this structure.
        /// </summary>
        public IField[] Fields { get; private set; }
        /// <summary>
        /// Value contained in the field.
        /// </summary>
        public string Value { get; private set; }
        /// <summary>
        /// Name of the field. Should not contain array dimensions or dots.
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// Fully qualified name of the field using dot notation.
        /// </summary>
        public string FullyQualifiedName
        {
            get
            {
                string name = Name;
                IField parent = Parent;
                while(parent != null && parent.Parent != null) // stop before adding name of "root" structure
                {
                    name = parent.Name + "." + name;
                    parent = parent.Parent;
                }
                return name;
            }
        }
        /// <summary>
        /// Field's parent (if any).
        /// </summary>
        public IField Parent { get; private set; }
        /// <summary>
        /// Field's parent's type. If Parent is null, returns "(this)".
        /// </summary>
        public string ParentType { get { return Parent == null ? "(this)" : Parent.TypeName; } }
        /// <summary>
        /// Name of the field's type.
        /// </summary>
        public string TypeName { get; private set; }
        /// <summary>
        /// Size of the field. If the field is a struct, this will be the sum of all contained fields.
        /// </summary>
        public short Size { get; private set; }

        /// <summary>
        /// Indicates if the field is a primitive; or, equivalently, indicates if was instantiated with a
        /// primitive type name.
        /// </summary>
        public bool IsPrimitive { get { return false; } }
        /// <summary>
        /// Indicates if the field is a pointer.
        /// </summary>
        public bool IsPointer { get { return false; } }
        /// <summary>
        /// Indicates if the field is a pointer to a pointer.
        /// </summary>
        public bool IsPointerToPointer { get { return false; } }
        /// <summary>
        /// Indicates if the field is an array.
        /// </summary>
        public bool IsArray { get { return false; } }
        /// <summary>
        /// Indicates if the field is a 2D array.
        /// </summary>
        public bool Is2DArray { get { return false; } }
        /// <summary>
        /// If the field is an array, returns the number of items in the field. Otherwise, returns 1.
        /// </summary>
        public int Length { get { return 1; } }

        private int pointerSize { get { return ContainingFile.PointerSize; } }

        /// <summary>
        /// Creates a Structure filled with data.
        /// </summary>
        /// <param name="data">Bytestream representing the data to be filled.</param>
        /// <param name="template">Structure prototype that matches the bytestream.</param>
        /// <param name="file">Source file object.</param>
        protected Structure(byte[] data, StructureDefinition template, BlenderFile file)
        {
            f.print($"\tParsing Structure: {template.StructureTypeName}. bytes: {data.Length} fields: {template.FieldList.Count} index: ?");
            // since this is a root structure, set its name to the type name (as an identification) and
            // Value to the type name (as expected by IField)
            Name = Value = TypeName = template.StructureTypeName;
            // roots have no parents
            Parent = null;
            Size = template.StructureTypeSize;

            List<IField> fields = new List<IField>();
            int pos = 0;
            this.ContainingFile = file;
            parseStructureFields(this, template, data, ref fields, ref pos); // begin recursive parsing
            Fields = fields.ToArray();
        }
        
        // embedded structure constructor, called by parseStructureFields
        private Structure(byte[] data, StructureDefinition template, string name, Structure parent)
            :this(data, template, parent.ContainingFile)
        {
            f.print($"\tEmbedded structure constructor {name} with bytes {data.Length} and parent {parent.Name}");
            Name = name;
            Parent = parent;
        }

        /// <summary>
        /// Recursively parses fields into flattened form.
        /// </summary>
        /// <param name="toParse">Structure to parse the fields of.</param>
        /// <param name="data">Data to input into fields.</param>
        /// <param name="position">Position of index in data.</param>
        /// <param name="fields">List of fields to add to.</param>
        /// <param name="parent">Parent of the fields being parsed.</param>
        private void parseStructureFields(Structure parent, StructureDefinition toParse, byte[] data, ref List<IField> fields, ref int position)
        {
            foreach(FieldDefinition field in toParse.FieldList)
            {
                f.print($"\t\tParsing Field: {field.Name}. size: ? at position {position}/{data.Length}");
                if(field.IsPointer) // it's almost a primitive
                {
                    
                    if(field.IsArray)
                    {
                       
                        // height and width are backwards, whoops
                        int height = getIntFromArrayName(field.Name);
                        int width = 1;
                        if(field.Name.Count(v => { return v == '['; }) == 1)
                            fields.Add(new Field<ulong[]>(toPointerArray(subArray(data, position, pointerSize * height)),
                                field.Name, field.Type.Name, (short)pointerSize, parent, pointerSize));
                        else
                        {
                            int start = field.Name.LastIndexOf('[');
                            int end = field.Name.LastIndexOf(']');
                            string numberString = field.Name.Substring(start + 1, end - 1 - start);
                            width = int.Parse(numberString);
                            fields.Add(new Field<ulong[][]>(to2DPointerArray(subArray(data, position, pointerSize * height * width), height),
                                field.Name, field.Type.Name, (short)pointerSize, parent, pointerSize));
                        }
                        position += pointerSize * width * height;
                    }
                    else
                    {
                        fields.Add(new Field<ulong>(toPointer(subArray(data, position, pointerSize)),
                            field.Name, field.Type.Name, (short)pointerSize, parent, pointerSize));
                        position += pointerSize;
                    }
                }
                else if(field.IsPrimitive)
                {
                   
                    if(field.IsArray)
                    {
                        int width = 1;
                        if(field.Name.Count(v => { return v == '['; }) == 1)
                            fields.Add(fieldFactory(subArray(data, position, field.Type.Size * getIntFromArrayName(field.Name)),
                                field.Name, field.Type.Name, field.Type.Size, parent, pointerSize));
                        else
                        {
                            int start = field.Name.LastIndexOf('[');
                            int end = field.Name.LastIndexOf(']');
                            string numberString = field.Name.Substring(start + 1, end - 1 - start);
                            width = int.Parse(numberString);
                            fields.Add(fieldFactory(subArray(data, position, field.Type.Size * getIntFromArrayName(field.Name) * width),
                                field.Name, field.Type.Name, field.Type.Size, parent, pointerSize));
                        }
                        position += field.Type.Size * width * getIntFromArrayName(field.Name);
                    }
                    else
                    {
                        fields.Add(fieldFactory(subArray(data, position, field.Type.Size), field.Name, field.Type.Name, field.Type.Size, parent, pointerSize));
                        position += field.Type.Size;
                    }
                }
                else // non-primitive
                {
                    
                    if(field.IsArray)
                    {
                        
                        // Unfortunately, I forgot about the case of non-primitives in arrays and didn't really plan for it
                        // with the format rewrite. One option would be to create some sort of class that inherits from
                        // IField and is just an array of PopulatedStructures. Another could be to cheese PopulatedStructure
                        // by making a "structure" with the required fields as data. I don't really like either of those ideas,
                        // so I'm going to cheese the system by just baking each array item into the structure as a seperate field.
                        // I don't like this idea much either, but it's the simplest solution and it's better than the others.

                        if(field.Name.Count(v => v == '[') == 1)
                            for(int i = 0; i < getIntFromArrayName(field.Name); i++)
                                fields.Add(new Structure(subArray(data, i * field.Type.Size, field.Type.Size), field.Structure, field.Name.Split('[')[0] + "[" + i + "]", parent));
                        else
                        {
                            // multidimensional arrays of non-primitives, can it get worse?
                            throw new Exception("gonna have to write this");
                            //int start = f.Name.LastIndexOf('[');
                            //int end = f.Name.LastIndexOf(']');
                            //string numberString = f.Name.Substring(start + 1, end - 1 - start);
                            //int width = int.Parse(numberString);
                            //for(int i = 0; i < getIntFromArrayName(f.Name); i++)
                            //    for(int j = 0; j < width; j++)
                            //        parseStructureFields((breadcrumbs == string.Empty ? "" : breadcrumbs + ".") + f.Name.Substring(0, f.Name.IndexOf('[')) + "[" + i + "]" + "[" + j + "]", f.Structure, data, ref position);
                        }
                    }
                    else
                    {
                        
                        fields.Add(new Structure(subArray(data, position, field.Structure.StructureTypeSize), field.Structure, field.Name.Split('[')[0], parent));
                        position += field.Structure.StructureTypeSize;
                    }
                }
                
            }
            f.print($"\t\tParsing Field Finished: {position}/{data.Length}");
            if(position - data.Length != 0)
               f.print($"\t\tParsing Field Error Unmatch:{toParse.StructureTypeName}");
        }

        private ulong toPointer(byte[] value)
        {
            if(value.Length < 1)
            {
                UnityEngine.Debug.LogError("toPointer: byte[] is empty");
                return 0;
            }
            return toPointer(value, 0);
        }

        private ulong toPointer(byte[] value, int index)
        {
            return pointerSize == 4 ? BitConverter.ToUInt32(value, index) : BitConverter.ToUInt64(value, index);
        }

        private ulong[] toPointerArray(byte[] value)
        {
            return toPointerArray(value, 0);
        }

        private ulong[] toPointerArray(byte[] value, int offset)
        {
            ulong[] output = new ulong[value.Length / pointerSize];
            for(int i = 0; i < output.Length; i++)
                output[i] = toPointer(value, i * pointerSize + offset);
            return output;
        }

        private ulong[][] to2DPointerArray(byte[] value, int dim1)
        {
            // infer dim2 by assuming a rectangular array
            int dim2 = (Value.Length / pointerSize) / dim1;
            ulong[][] output = new ulong[dim1][];
            for(int i = 0; i < output.Length; i++)
                output[i] = toPointerArray(value, i * pointerSize * dim2);
            return output;
        }

        private IField fieldFactory(byte[] value, string fieldName, string fieldType, short fieldSize, IField parent, int pointerSize)
        {
            // this is where I get to make big if statements for each generic; it had to happen sometime
            if(fieldName.Contains('['))
            {
                int count = fieldName.Count(c => c == '[');
                if(count == 2)
                {
                    // I love that I can nest fieldFactoryArrayHelper calls. Unintended side effect.
                    if(fieldType == "char")
                        return new Field<char[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => Encoding.ASCII.GetChars(value, j + i, 1)[0])), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "uchar")
                        return new Field<byte[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => value[i + j])), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "short")
                        return new Field<short[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToInt16(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "ushort")
                        return new Field<ushort[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToUInt16(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "int")
                        return new Field<int[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToInt32(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "long") // C longs are arch-dependant and should vary with the pointer size.
                        return pointerSize == 4 ? (IField)new Field<int[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToInt32(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize) :
                                                  (IField)new Field<long[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToInt64(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "ulong")
                        return pointerSize == 4 ? (IField)new Field<uint[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToUInt32(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize) :
                                                  (IField)new Field<ulong[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToUInt64(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "float")
                        return new Field<float[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToSingle(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "double")
                        return new Field<double[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToDouble(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "int64_t")
                        return new Field<long[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToInt64(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "uint64_t")
                        return new Field<ulong[][]>(fieldFactoryArrayHelper(value, fieldSize * fieldSize, i => fieldFactoryArrayHelper(subArray(value, i, value.Length / getIntFromArrayName(fieldName)), fieldSize, j => BitConverter.ToUInt64(value, i + j))), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "void") // probably a pointer
                        return new Field<ulong[][]>(to2DPointerArray(value, getIntFromArrayName(fieldName)), fieldName, fieldType, fieldSize, parent, pointerSize);
                }
                else if(count == 1)
                {
                    if(fieldType == "char")
                        return new Field<char[]>(fieldFactoryArrayHelper(value, fieldSize, i => Encoding.ASCII.GetChars(value, i, 1)[0]), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "uchar")
                        return new Field<byte[]>(fieldFactoryArrayHelper(value, fieldSize, i => value[i]), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "short")
                        return new Field<short[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToInt16(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "ushort")
                        return new Field<ushort[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToUInt16(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "int")
                        return new Field<int[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToInt32(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "long") // C longs are arch-dependant and should vary with the pointer size.
                        return pointerSize == 4 ? (IField)new Field<int[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToInt32(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize) :
                                                  (IField)new Field<long[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToInt64(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "ulong")
                        return pointerSize == 4 ? (IField)new Field<uint[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToUInt32(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize) :
                                                  (IField)new Field<ulong[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToUInt64(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "float")
                        return new Field<float[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToSingle(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "double")
                        return new Field<double[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToDouble(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "int64_t")
                        return new Field<long[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToInt64(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "uint64_t")
                        return new Field<ulong[]>(fieldFactoryArrayHelper(value, fieldSize, i => BitConverter.ToUInt64(value, i)), fieldName, fieldType, fieldSize, parent, pointerSize);
                    else if(fieldType == "void") // probably a pointer
                        return new Field<ulong[]>(toPointerArray(value), fieldName, fieldType, fieldSize, parent, pointerSize);
                }
                else
                    throw new InvalidOperationException("Library doesn't support 3D arrays, it'll need a patch.");
            }
            else
            {
                if(fieldType == "char")
                    return new Field<char>(Encoding.ASCII.GetChars(value, 0, 1)[0], fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "uchar")
                    return new Field<byte>(value[0], fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "short")
                    return new Field<short>(BitConverter.ToInt16(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "ushort")
                    return new Field<ushort>(BitConverter.ToUInt16(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "int")
                    return new Field<int>(BitConverter.ToInt32(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "long") // C longs are arch-dependant and should vary with the pointer size.
                    return pointerSize == 4 ? (IField)new Field<int>(BitConverter.ToInt32(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize) :
                                              (IField)new Field<long>(BitConverter.ToInt64(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "ulong")
                    return pointerSize == 4 ? (IField)new Field<uint>(BitConverter.ToUInt32(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize) :
                                              (IField)new Field<ulong>(BitConverter.ToUInt64(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "float")
                    return new Field<float>(BitConverter.ToSingle(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "double")
                    return new Field<double>(BitConverter.ToDouble(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "int8_t")
                    return new Field<sbyte[]>(Array.ConvertAll(value, b => unchecked((sbyte)b)), fieldName, fieldType, fieldSize, parent, pointerSize); // Added int8_t support
                else if(fieldType == "int64_t")
                    return new Field<long>(BitConverter.ToInt64(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "uint64_t")
                    return new Field<ulong>(BitConverter.ToUInt64(value, 0), fieldName, fieldType, fieldSize, parent, pointerSize);
                else if(fieldType == "void") // probably a pointer
                    return new Field<ulong>(toPointer(value), fieldName, fieldType, fieldSize, parent, pointerSize);
                    
            }
            UnityEngine.Debug.Log($"Bad field type name. {fieldType}");
            return null;
        }

        private T[] fieldFactoryArrayHelper<T>(byte[] value, int size, Func<int, T> converter)
        {
            T[] output = new T[value.Length / size];

            for(int i = 0; i < output.Length; i++)
                output[i] = converter(size * i);
            
            return output;
        }

        // helper function to "slice" arrays
        private byte[] subArray(byte[] original, int start, int length)
        {
            byte[] output = new byte[length];
            for(int i = 0; i < length; i++)
                output[i] = original[start + i];
            return output;
        }

        // helper function to get array size from field name
        private int getIntFromArrayName(string name)
        {
            int start = name.IndexOf('[');
            int end = name.IndexOf(']');
            string numberString = name.Substring(start + 1, end - 1 - start);
            int number = int.Parse(numberString);
            return number;
        }

        /// <summary>
        /// Array-style access to fields.
        /// </summary>
        /// <param name="identifier">Name of the field you want to look up.</param>
        /// <returns>IField object representing the field with that identifier</returns>
        public IField this[string identifier]
        {
            get
            {
                string[] parts = identifier.Split('.');
                try
                {
                    int index = 0;
                    IStructField parent = this;
                    // this loop will fail if we access members of a primitive or have a bad name somewhere
                    while(index < parts.Length - 1) // exhaust all dot-following, leaving only the final identifier
                        parent = (IStructField)parent[parts[index++]];
                    return parent.Fields.First(f => f.Name == parts[index]);
                }
                catch
                {
                    throw new ArgumentException("Identifier " + identifier + " not found.", identifier);
                }
            }
        }

        /// <summary>
        /// If the represented field is a pointer, dereferences it and returns the pointed-to structure(s).
        /// If the field is not a pointer, throws <pre>InvalidOperationException</pre>. If the pointer is null,
        /// returns null. If the field is a pointer to a pointer or an array of pointers throws InvalidDataException.
        /// </summary>
        /// <returns>A <pre>PopulatedStructure</pre> pointed to by the field, or null.</returns>
        public Structure[] Dereference()
        {
            throw new InvalidOperationException("This field is not a pointer.");
        }

        /// <summary>
        /// If the represented field is an array of pointers, dereferences them and returns an array of references.
        /// For null pointers, null is used. If the represented field is not an array of pointers, throws InvalidOperationException.
        /// If the field is a pointer to a pointer, throws InvalidDataException.
        /// </summary>
        /// <returns>An array of <pre>PopulatedStructure</pre>s.</returns>
        public Structure[][] DereferenceAsArray()
        {
            throw new InvalidOperationException("This field is not a pointer.");
        }

        /// <summary>
        /// Iterates over all fields in the structure, flattening as necessary.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<IField> GetEnumerator()
        {
            Structure p;
            foreach(IField field in Fields)
                if((p = field as Structure) != null)
                    foreach(IField inner in p)
                        yield return inner;
                else
                    yield return field;
        }

        /// <summary>
        /// Iterates over all fields in the structure, flattening as necessary.
        /// </summary>
        /// <returns></returns>
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
