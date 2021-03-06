﻿// Copyright (c) 2014 Silicon Studio Corp. (http://siliconstudio.co.jp)
// This file is distributed under GPL v3. See LICENSE.md for details.
using System;
using System.Linq;

using SiliconStudio.ActionStack;
using SiliconStudio.Core.Reflection;
using SiliconStudio.Quantum.Attributes;

namespace SiliconStudio.Quantum.Commands
{
    public class AddPrimitiveKeyCommand : INodeCommand
    {
        /// <inheritdoc/>
        public string Name { get { return "AddPrimitiveKey"; } }

        /// <inheritdoc/>
        public CombineMode CombineMode { get { return CombineMode.CombineOnlyForAll; } }
        
        /// <inheritdoc/>
        public bool CanAttach(ITypeDescriptor typeDescriptor, MemberDescriptorBase memberDescriptor)
        {
            if (memberDescriptor != null)
            {
                var attrib = TypeDescriptorFactory.Default.AttributeRegistry.GetAttribute<SealedCollectionAttribute>(memberDescriptor.MemberInfo);
                if (attrib != null && attrib.CollectionSealed)
                    return false;
            }
            
            var dictionaryDescriptor = typeDescriptor as DictionaryDescriptor;
            if (dictionaryDescriptor == null)
                return false;
            return !dictionaryDescriptor.KeyType.IsClass || dictionaryDescriptor.KeyType == typeof(string) || dictionaryDescriptor.KeyType.GetConstructor(new Type[0]) != null;
        }

        /// <inheritdoc/>
        public object Invoke(object currentValue, ITypeDescriptor descriptor, object parameter, out UndoToken undoToken)
        {
            var dictionaryDescriptor = (DictionaryDescriptor)descriptor;
            var newKey = dictionaryDescriptor.KeyType != typeof(string) ? Activator.CreateInstance(dictionaryDescriptor.KeyType) : GenerateStringKey(currentValue, descriptor, parameter);
            var newItem = !dictionaryDescriptor.ValueType.IsAbstract ? Activator.CreateInstance(dictionaryDescriptor.ValueType) : null;
            dictionaryDescriptor.SetValue(currentValue, newKey, newItem);
            undoToken = new UndoToken(true, newKey);
            return currentValue;
        }

        /// <inheritdoc/>
        public object Undo(object currentValue, ITypeDescriptor descriptor, UndoToken undoToken)
        {
            var dictionaryDescriptor = (DictionaryDescriptor)descriptor;
            var key = undoToken.TokenValue;
            dictionaryDescriptor.Remove(currentValue, key);
            return currentValue;
        }

        private static object GenerateStringKey(object value, ITypeDescriptor descriptor, object baseValue)
        {
            // TODO: use a dialog service and popup a message when the given key is invalid
            string baseName = GenerateBaseName(baseValue);
            int i = 1;

            var dictionary = (DictionaryDescriptor)descriptor;
            while (dictionary.ContainsKey(value, baseName))
            {
                baseName = (baseValue != null ? baseValue.ToString() : "Key") + " " + ++i;
            }

            return baseName;
        }

        private static string GenerateBaseName(object baseValue)
        {
            const string DefaultKey = "Key";

            if (baseValue == null)
                return DefaultKey;
            
            var baseName = baseValue.ToString();
            if (string.IsNullOrWhiteSpace(baseName))
                return DefaultKey;

            if (baseName.Any(x => !Char.IsLetterOrDigit(x) && x == ' ' && x == '_'))
                return DefaultKey;

            return baseName;
        }
    }
}
