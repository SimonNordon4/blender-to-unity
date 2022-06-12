﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace BlenderFileReader
{
    /// <summary>
    /// Represents the file's StructureDNA.
    /// </summary>
    public class StructureDNA : FileBlock
    {
        /// <summary>
        /// List of all the names contained in SDNA.
        /// </summary>
        public List<string> NameList { get; private set; }
        /// <summary>
        /// List of all the types and their sizes contained in SDNA.
        /// </summary>
        public List<TypeDefinition> TypeList { get; private set; }
        /// <summary>
        /// List of all the names of the types in SDNA; used primarily for BlenderType and BlenderField's constructors.
        /// </summary>
        public List<string> TypeNameList { get; private set; }
        /// <summary>
        /// List of all structures defined in SDNA.
        /// </summary>
        public List<StructureDefinition> StructureList { get; private set; }
        /// <summary>
        /// List of all of the structures' types by index in TypeList/TypeNameList; used primarily for BlenderType and BlenderField's constructors.
        /// </summary>
        public List<short> StructureTypeIndices { get; private set; }

        /// <summary>
        /// Creates a new StructureDNA block.
        /// </summary>
        /// <param name="code">Fileblock code (should be "DNA1")</param>
        /// <param name="size">Block size.</param>
        /// <param name="sdna">SDNA index (should be 0)</param>
        /// <param name="count">FileBlock count (should be 1)</param>
        /// <param name="data">Bytestream representing the SDNA.</param>
        public StructureDNA(string code, int size, int sdna, int count, byte[] data)
            : base(code, size, sdna, count, data)
        {
            int position = 0;
            position += 4; // read 'SDNA'
            
            position = readNames(position);
            
            while(position % 4 != 0) // next field is aligned at four bytes
                position++;

            int numberOfTypes = readTypes(ref position);

            while(position % 4 != 0) // next field is aligned at four bytes
                position++;

            List<short> typeLengthList = readTypeLengths(ref position, numberOfTypes);

            while(position % 4 != 0) // next field is aligned at four bytes
                position++;

            int numberOfStructures;
            List<Dictionary<short, short>> structureFields;
            position = readStructures(position, out numberOfStructures, out structureFields);

            // now that we've read out everything we'll need, create the objects
            TypeList = new List<TypeDefinition>();
            for(int i = 0; i < numberOfTypes; i++)
                TypeList.Add(new TypeDefinition(TypeNameList[i], typeLengthList[i], this));

            StructureList = new List<StructureDefinition>(numberOfStructures);
            for(int i = 0; i < numberOfStructures; i++)
            {
                List<FieldDefinition> fields = new List<FieldDefinition>();
                for(int j = 0; j < structureFields[i].Count; j++)
                {
                    KeyValuePair<short, short> element = structureFields[i].ElementAt(j);
                    fields.Add(new FieldDefinition(element.Key, element.Value, this));
                }
                StructureList.Add(new StructureDefinition(StructureTypeIndices[i], fields, this));
            }

            // finish lazy initialization of the structures
            foreach(StructureDefinition s in StructureList)
                s.InitializeFields();
        }

        private int readStructures(int position, out int numberOfStructures, out List<Dictionary<short, short>> structureFields)
        {
            char[] type;

            type = new[] { Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0],  // read structure ID to check
                            Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0] };
            if(type[0] != 'S' || type[1] != 'T' || type[2] != 'R' || type[3] != 'C')
                throw new InvalidOperationException("Failed reading SDNA: structures could not be read.");

            numberOfStructures = BitConverter.ToInt32(Data, position);
            position += 4; // you know the drill

            StructureTypeIndices = new List<short>();
            structureFields = new List<Dictionary<short, short>>();
            for(int i = 0; i < numberOfStructures; i++)
            {
                short structureTypeIndex = BitConverter.ToInt16(Data, position);
                position += 2;
                short numberOfFields = BitConverter.ToInt16(Data, position);
                position += 2;
                Dictionary<short, short> fieldDict = new Dictionary<short, short>();
                for(int j = 0; j < numberOfFields; j++)
                {
                    short typeOfField = BitConverter.ToInt16(Data, position);
                    position += 2;
                    short name = BitConverter.ToInt16(Data, position);
                    position += 2;
                    fieldDict.Add(name, typeOfField);
                }
                StructureTypeIndices.Add(structureTypeIndex);
                structureFields.Add(fieldDict);
            }
            return position;
        }

        private List<short> readTypeLengths(ref int position, int numberOfTypes)
        {
            char[] type;

            type = new[] { Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0],  // read tlen ID to check
                            Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0] };
            if(type[0] != 'T' || type[1] != 'L' || type[2] != 'E' || type[3] != 'N')
                throw new InvalidOperationException("Failed reading SDNA: type lengths could not be read.");

            List<short> typeLengthList = new List<short>(numberOfTypes);
            for(int i = 0; i < numberOfTypes; i++)
            {
                typeLengthList.Add(BitConverter.ToInt16(Data, position));
                position += 2; // add to position to reflect the read
            }
            return typeLengthList;
        }

        private int readTypes(ref int position)
        {
            char[] type;
            List<char> tempCharList = new List<char>();

            type = new[] { Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0],  // read type ID to check
                            Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0] };
            if(type[0] != 'T' || type[1] != 'Y' || type[2] != 'P' || type[3] != 'E')
                throw new InvalidOperationException("Failed reading SDNA: types could not be read.");

            int numberOfTypes = BitConverter.ToInt32(Data, position);
            position += 4; // increment position to reflect the read

            TypeNameList = new List<string>(numberOfTypes);
            for(int i = 0; i < numberOfTypes; i++)
            {
                char c;
                do
                {
                    c = Encoding.ASCII.GetChars(Data, position++, 1)[0];
                    tempCharList.Add(c);
                } while(c != '\0');
                tempCharList.RemoveAt(tempCharList.Count - 1); // removes terminating zero
                TypeNameList.Add(new string(tempCharList.ToArray()));
                tempCharList.Clear();
            }
            return numberOfTypes;
        }

        private int readNames(int position)
        {
            char[] type;
            List<char> tempCharList;

            type = new char[]{ Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0],  // read name ID to check
                            Encoding.ASCII.GetChars(Data, position++, 1)[0], Encoding.ASCII.GetChars(Data, position++, 1)[0] };
            if(type[0] != 'N' || type[1] != 'A' || type[2] != 'M' || type[3] != 'E')
                throw new InvalidOperationException("Failed reading SDNA: names could not be read.");

            int numberOfNames = BitConverter.ToInt32(Data, position);
            position += 4; // make sure to increment position
            while(position % 4 != 0) // next field is aligned at four bytes
                position++;

            NameList = new List<string>(numberOfNames);
            tempCharList = new List<char>();
            for(int i = 0; i < numberOfNames; i++)
            {
                char c;
                do
                {
                    c = Encoding.ASCII.GetChars(Data, position++, 1)[0];
                    tempCharList.Add(c);
                } while(c != '\0');
                tempCharList.RemoveAt(tempCharList.Count - 1); // removes terminating zero
                NameList.Add(new string(tempCharList.ToArray()));
                tempCharList.Clear();
            }
            return position;
        }
    }
}
