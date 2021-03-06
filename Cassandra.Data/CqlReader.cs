﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Data.Common;
using System.Collections;
using Cassandra;

namespace Cassandra.Data
{
    public class CqlReader : DbDataReader
    {
        CqlRowSet popul = null;
        IEnumerable<CqlRow> enumRows = null;
        IEnumerator<CqlRow> enumerRows = null;
        Dictionary<string, int> colidx = new Dictionary<string, int>();
        internal CqlReader(CqlRowSet rows)
        {
            this.popul = rows;
            for (int idx = 0; idx < popul.Columns.Length; idx++)
                colidx.Add(popul.Columns[idx].Name, idx);
            enumRows = popul.GetRows();
            enumerRows = enumRows.GetEnumerator();
        }

        public override void Close()
        {
            popul.Dispose();
        }

        public override int Depth
        {
            get { return 0; }
        }

        public override int FieldCount
        {
            get { return popul.Columns.Length; }
        }

        public override bool GetBoolean(int ordinal)
        {
            return (bool)GetValue(ordinal);
        }

        public override byte GetByte(int ordinal)
        {
            return (byte)GetValue(ordinal);
        }

        public override long GetBytes(int ordinal, long dataOffset, byte[] buffer, int bufferOffset, int length)
        {
            throw new NotSupportedException();
        }

        public override char GetChar(int ordinal)
        {
            return (char)GetValue(ordinal);
        }

        public override long GetChars(int ordinal, long dataOffset, char[] buffer, int bufferOffset, int length)
        {
            throw new NotSupportedException();
        }

        public override string GetDataTypeName(int ordinal)
        {
            return popul.Columns[ordinal].TypeCode.ToString();
        }

        public override DateTime GetDateTime(int ordinal)
        {
            return (DateTime)GetValue(ordinal);
        }

        public override decimal GetDecimal(int ordinal)
        {
            return (decimal)GetValue(ordinal);
        }

        public override double GetDouble(int ordinal)
        {
            return (double)GetValue(ordinal);
        }

        public override System.Collections.IEnumerator GetEnumerator()
        {
            return ((IEnumerator)new DbEnumerator(this));
        }

        public override Type GetFieldType(int ordinal)
        {
            return popul.Columns[ordinal].Type;
        }

        public override float GetFloat(int ordinal)
        {
            return (float)GetValue(ordinal);
        }

        public override Guid GetGuid(int ordinal)
        {
            return (Guid)GetValue(ordinal);
        }

        public override short GetInt16(int ordinal)
        {
            return (Int16)GetValue(ordinal);
        }

        public override int GetInt32(int ordinal)
        {
            return (Int32)GetValue(ordinal);
        }

        public override long GetInt64(int ordinal)
        {
            return (Int64)GetValue(ordinal);
        }

        public override string GetName(int ordinal)
        {
            return popul.Columns[ordinal].Name;
        }

        public override int GetOrdinal(string name)
        {
            return colidx[name];
        }

        public override DataTable GetSchemaTable()
        {
            throw new NotSupportedException();
        }

        public override string GetString(int ordinal)
        {
            return (string)GetValue(ordinal);
        }

        public override object GetValue(int ordinal)
        {
            return enumerRows.Current[ordinal];
        }

        public override int GetValues(object[] values)
        {
            Array.Copy(enumerRows.Current.Columns, values,enumerRows.Current.Columns.Length);
            return enumerRows.Current.Columns.Length;
        }

        public override bool HasRows
        {
            get { return popul.RowsCount > 0; }
        }

        public override bool IsClosed
        {
            get { return false; }
        }

        public override bool IsDBNull(int ordinal)
        {
            return enumerRows.Current.IsNull(ordinal);
        }

        public override bool NextResult()
        {
            return enumerRows.MoveNext();
        }

        public override bool Read()
        {
            return enumerRows.MoveNext();
        }

        public override int RecordsAffected
        {
            get { return -1; }
        }

        public override object this[string name]
        {
            get { return GetValue(GetOrdinal(name)); }
        }

        public override object this[int ordinal]
        {
            get { return GetValue(ordinal); }
        }
    }
}
