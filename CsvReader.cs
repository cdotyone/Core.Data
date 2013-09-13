using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Text;

namespace Civic.Core.Data
{
	public class CsvReader : IDataReader
	{
		private FileStream _fileStream;
		private Stream _stream;
		private StreamReader _streamReader;
		private StreamWriter _streamWriter;
		private Stream _memoryStream;
		private Encoding _encoding;
		private readonly StringBuilder _columnBuilder = new StringBuilder(100);
		private readonly Type _type = Type.File;
		private List<string> _currentRow;
		private int _lineCount = 0;

		#region Enums

		/// <summary>
		/// Type enum
		/// </summary>
		private enum Type
		{
			File,
			Stream,
			String
		}

		#endregion Enums

		#region Constructors

		/// <summary>
		/// Initialises the reader to work from a file
		/// </summary>
		/// <param name="filePath">File path</param>
		/// <param name="hasHeaderRow">Does the first line in the file contain the has header row</param>
		public CsvReader(string filePath, bool hasHeaderRow = true)
		{
			Depth = 0;
			RecordsAffected = 0;
			_type = Type.File;
			HasHeaderRow = hasHeaderRow;
			TrimColumns = true;

			initialize(filePath, Encoding.Default);
		}

		/// <summary>
		/// Initialises the reader to work from a file
		/// </summary>
		/// <param name="filePath">File path</param>
		/// <param name="encoding">Encoding</param>
		/// <param name="hasHeaderRow">Does the first line in the file contain the has header row</param>
		public CsvReader(string filePath, Encoding encoding, bool hasHeaderRow = true)
		{
			Depth = 0;
			RecordsAffected = 0;
			_type = Type.File;
			HasHeaderRow = hasHeaderRow;
			TrimColumns = true;

			initialize(filePath, encoding);
		}

		/// <summary>
		/// Initialises the reader to work from an existing stream
		/// </summary>
		/// <param name="stream">Stream</param>
		/// <param name="hasHeaderRow">Does the first line in the file contain the has header row</param>
		public CsvReader(Stream stream, bool hasHeaderRow = true)
		{
			Depth = 0;
			RecordsAffected = 0;
			_type = Type.Stream;
			HasHeaderRow = hasHeaderRow;

			initialize(stream, Encoding.Default);
		}

		/// <summary>
		/// Initialises the reader to work from an existing stream
		/// </summary>
		/// <param name="stream">Stream</param>
		/// <param name="encoding">Encoding</param>
		/// <param name="hasHeaderRow">Does the first line in the file contain the has header row</param>
		public CsvReader(Stream stream, Encoding encoding, bool hasHeaderRow = true)
		{
			Depth = 0;
			RecordsAffected = 0;
			_type = Type.Stream;
			HasHeaderRow = hasHeaderRow;
			TrimColumns = true;

			initialize(stream, encoding);
		}

		/// <summary>
		/// Initialises the reader to work from a csv string
		/// </summary>
		/// <param name="encoding"></param>
		/// <param name="csvContent"></param>
		/// <param name="hasHeaderRow">Does the first line in the file contain the has header row</param>
		public CsvReader(Encoding encoding, string csvContent, bool hasHeaderRow = true)
		{
			Depth = 0;
			RecordsAffected = 0;
			_type = Type.String;
			HasHeaderRow = hasHeaderRow;
			TrimColumns = true;

			initialize(encoding, csvContent);
		}

		/// <summary>
		/// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Names = null;
			_currentRow = null;

			closeStreams();
		}


		/// <summary>
		/// Close all streams and release resources
		/// </summary>
		private void closeStreams()
		{
			if (_streamReader != null)
			{
				_streamReader.Close();
				_streamReader.Dispose();
			}

			if (_streamWriter != null)
			{
				_streamWriter.Close();
				_streamWriter.Dispose();
			}

			if (_memoryStream != null)
			{
				_memoryStream.Close();
				_memoryStream.Dispose();
			}

			if (_fileStream != null)
			{
				_fileStream.Close();
				_fileStream.Dispose();
			}

			if ((_type == Type.String || _type == Type.File) && _stream != null)
			{
				_stream.Close();
				_stream.Dispose();
			}
		}

		#endregion Constructors


		#region Methods

		/// <summary>
		/// Initialises the class to use a file
		/// </summary>
		/// <param name="filePath"></param>
		/// <param name="encoding"></param>
		private void initialize(string filePath, Encoding encoding)
		{
			if (!File.Exists(filePath))
				throw new FileNotFoundException(string.Format("The file '{0}' does not exist.", filePath));

			_fileStream = File.OpenRead(filePath);
			initialize(_fileStream, encoding);
		}

		/// <summary>
		/// Initialises the class to use a stream
		/// </summary>
		/// <param name="stream"></param>
		/// <param name="encoding"></param>
		private void initialize(Stream stream, Encoding encoding)
		{
			if (stream == null)
				throw new ArgumentNullException("stream", "The supplied stream is null.");

			_stream = stream;
			_stream.Position = 0;
			_encoding = (encoding ?? Encoding.Default);
			_streamReader = new StreamReader(_stream, _encoding);

			if (HasHeaderRow)
			{
				if (Read())
				{
					Names = _currentRow;
					_currentRow = null;
				}
			}
		}

		/// <summary>
		/// Initialies the class to use a string
		/// </summary>
		/// <param name="encoding"></param>
		/// <param name="csvContent"></param>
		private void initialize(Encoding encoding, string csvContent)
		{
			if (csvContent == null)
				throw new ArgumentNullException("csvContent", "The supplied csvContent is null.");

			_encoding = (encoding ?? Encoding.Default);

			_memoryStream = new MemoryStream(csvContent.Length);
			_streamWriter = new StreamWriter(_memoryStream);
			_streamWriter.Write(csvContent);
			_streamWriter.Flush();
			initialize(_memoryStream, encoding);
		}

		/// <summary>
		/// Parses a csv line
		/// </summary>
		/// <param name="line">Line</param>
		private void parseLine(string line)
		{
			_currentRow = new List<string>();
			bool inColumn = false;
			bool inQuotes = false;
			_columnBuilder.Remove(0, _columnBuilder.Length);

			// Iterate through every character in the line
			for (int i = 0; i < line.Length; i++)
			{
				char character = line[i];

				// If we are not currently inside a column
				if (!inColumn)
				{
					// If the current character is a double quote then the column value is contained within
					// double quotes, otherwise append the next character
					if (character == '"')
						inQuotes = true;
					else
					{
						if (character == ',' && !inQuotes)
						{
							_currentRow.Add("");
							continue;
						}
						else _columnBuilder.Append(character);
					}

					inColumn = true;
					continue;
				}

				// If we are in between double quotes
				if (inQuotes)
				{
					// If the current character is a double quote and the next character is a comma or we are at the end of the line
					// we are now no longer within the column.
					// Otherwise increment the loop counter as we are looking at an escaped double quote e.g. "" within a column
					if (character == '"' && ((line.Length > (i + 1) && line[i + 1] == ',') || ((i + 1) == line.Length)))
					{
						inQuotes = false;
						inColumn = false;
						i++;
					}
					else if (character == '"' && line.Length > (i + 1) && line[i + 1] == '"')
						i++;
				}
				else if (character == ',')
					inColumn = false;

				// If we are no longer in the column clear the builder and add the columns to the list
				if (!inColumn)
				{
					_currentRow.Add(TrimColumns ? _columnBuilder.ToString().Trim(new []{' ','"'}) : _columnBuilder.ToString());
					_columnBuilder.Remove(0, _columnBuilder.Length);
				}
				else // append the current column
					_columnBuilder.Append(character);
			}

			// If we are still inside a column add a new one
			if (inColumn)
				_currentRow.Add(TrimColumns ? _columnBuilder.ToString().Trim() : _columnBuilder.ToString());
		}

		#endregion Methods

		#region IDataReader

		/// <summary>
		/// Gets the name for the field to find.
		/// </summary>
		/// <returns>
		/// The name of the field or the empty string (""), if there is no value to return.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public string GetName(int i)
		{
			if (i >= 0 && Names != null && Names.Count > i) return Names[i];
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the data type information for the specified field.
		/// </summary>
		/// <returns>
		/// The data type information for the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public string GetDataTypeName(int i)
		{
			return "string";
		}

		/// <summary>
		/// Gets the <see cref="T:System.Type"/> information corresponding to the type of <see cref="T:System.Object"/> that would be returned from <see cref="M:System.Data.IDataRecord.GetValue(System.Int32)"/>.
		/// </summary>
		/// <returns>
		/// The <see cref="T:System.Type"/> information corresponding to the type of <see cref="T:System.Object"/> that would be returned from <see cref="M:System.Data.IDataRecord.GetValue(System.Int32)"/>.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public System.Type GetFieldType(int i)
		{
			return typeof(string);
		}

		/// <summary>
		/// Return the value of the specified field.
		/// </summary>
		/// <returns>
		/// The <see cref="T:System.Object"/> which will contain the field value upon return.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public object GetValue(int i)
		{
			return getStringValue(i);
		}

		private string getStringValue(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i) return _currentRow[i];
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Populates an array of objects with the column values of the current record.
		/// </summary>
		/// <returns>
		/// The number of instances of <see cref="T:System.Object"/> in the array.
		/// </returns>
		/// <param name="values">An array of <see cref="T:System.Object"/> to copy the attribute fields into. </param><filterpriority>2</filterpriority>
		public int GetValues(object[] values)
		{
			if(_currentRow==null || _currentRow.Count==0) throw new Exception("There is no current row");

			int min = Math.Min(_currentRow.Count, values.Length);

			if(min<=0) throw new Exception("The destination array parameter does not have any elements");

			for (var i = 0; i < min; i++)
				values[i] = _currentRow[0];

			return min;
		}

		/// <summary>
		/// Return the index of the named field.
		/// </summary>
		/// <returns>
		/// The index of the named field.
		/// </returns>
		/// <param name="name">The name of the field to find. </param><filterpriority>2</filterpriority>
		public int GetOrdinal(string name)
		{
			if(string.IsNullOrEmpty(name))
				throw new ArgumentNullException("name","name argument can not be null or empty");
			if (Names != null && Names.Count > 0) return Names.IndexOf(name);
			return -1;
		}

		/// <summary>
		/// Gets the value of the specified column as a Boolean.
		/// </summary>
		/// <returns>
		/// The value of the column.
		/// </returns>
		/// <param name="i">The zero-based column ordinal. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public bool GetBoolean(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				return _currentRow[i].ToLower() == "true" || _currentRow[i] == "1";
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the 8-bit unsigned integer value of the specified column.
		/// </summary>
		/// <returns>
		/// The 8-bit unsigned integer value of the specified column.
		/// </returns>
		/// <param name="i">The zero-based column ordinal. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public byte GetByte(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				return Encoding.UTF8.GetBytes(_currentRow[i])[0];
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Reads a stream of bytes from the specified column offset into the buffer as an array, starting at the given buffer offset.
		/// </summary>
		/// <returns>
		/// The actual number of bytes read.
		/// </returns>
		/// <param name="i">The zero-based column ordinal. </param><param name="fieldOffset">The index within the field from which to start the read operation. </param><param name="buffer">The buffer into which to read the stream of bytes. </param><param name="bufferoffset">The index for <paramref name="buffer"/> to start the read operation. </param><param name="length">The number of bytes to read. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				return Encoding.UTF8.GetBytes(_currentRow[i], (int)fieldOffset, length, buffer, bufferoffset);
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the character value of the specified column.
		/// </summary>
		/// <returns>
		/// The character value of the specified column.
		/// </returns>
		/// <param name="i">The zero-based column ordinal. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public char GetChar(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				return _currentRow[i][0];
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Reads a stream of characters from the specified column offset into the buffer as an array, starting at the given buffer offset.
		/// </summary>
		/// <returns>
		/// The actual number of characters read.
		/// </returns>
		/// <param name="i">The zero-based column ordinal. </param><param name="fieldoffset">The index within the row from which to start the read operation. </param><param name="buffer">The buffer into which to read the stream of bytes. </param><param name="bufferoffset">The index for <paramref name="buffer"/> to start the read operation. </param><param name="length">The number of bytes to read. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				Array.Copy(_currentRow[i].ToCharArray(),fieldoffset,buffer,bufferoffset,length);
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Returns the GUID value of the specified field.
		/// </summary>
		/// <returns>
		/// The GUID value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public Guid GetGuid(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				return Guid.Parse(_currentRow[i]);
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the 16-bit signed integer value of the specified field.
		/// </summary>
		/// <returns>
		/// The 16-bit signed integer value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public short GetInt16(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				Int16 val;
				Int16.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the 32-bit signed integer value of the specified field.
		/// </summary>
		/// <returns>
		/// The 32-bit signed integer value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public int GetInt32(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				Int32 val;
				Int32.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the 64-bit signed integer value of the specified field.
		/// </summary>
		/// <returns>
		/// The 64-bit signed integer value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public long GetInt64(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				Int64 val;
				Int64.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the single-precision floating point number of the specified field.
		/// </summary>
		/// <returns>
		/// The single-precision floating point number of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public float GetFloat(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				float val;
				float.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the double-precision floating point number of the specified field.
		/// </summary>
		/// <returns>
		/// The double-precision floating point number of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public double GetDouble(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				double val;
				double.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the string value of the specified field.
		/// </summary>
		/// <returns>
		/// The string value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public string GetString(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				return _currentRow[i];
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the fixed-position numeric value of the specified field.
		/// </summary>
		/// <returns>
		/// The fixed-position numeric value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public decimal GetDecimal(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				decimal val;
				decimal.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the date and time data value of the specified field.
		/// </summary>
		/// <returns>
		/// The date and time data value of the specified field.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public DateTime GetDateTime(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
			{
				DateTime val;
				DateTime.TryParse(_currentRow[i], out val);
				return val;
			}
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Returns an <see cref="T:System.Data.IDataReader"/> for the specified column ordinal.
		/// </summary>
		/// <returns>
		/// An <see cref="T:System.Data.IDataReader"/>.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public IDataReader GetData(int i)
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Return whether the specified field is set to null.
		/// </summary>
		/// <returns>
		/// true if the specified field is set to null; otherwise, false.
		/// </returns>
		/// <param name="i">The index of the field to find. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		public bool IsDBNull(int i)
		{
			if (i >= 0 && _currentRow != null && _currentRow.Count > i)
				return string.IsNullOrEmpty(_currentRow[i]);
			throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
		}

		/// <summary>
		/// Gets the number of columns in the current row.
		/// </summary>
		/// <returns>
		/// When not positioned in a valid recordset, 0; otherwise, the number of columns in the current record. The default is -1.
		/// </returns>
		/// <filterpriority>2</filterpriority>
		public int FieldCount {
			get { return _currentRow != null ? _currentRow.Count : Names.Count; }
		}

		/// <summary>
		/// Gets the column located at the specified index.
		/// </summary>
		/// <returns>
		/// The column located at the specified index as an <see cref="T:System.Object"/>.
		/// </returns>
		/// <param name="i">The zero-based index of the column to get. </param><exception cref="T:System.IndexOutOfRangeException">The index passed was outside the range of 0 through <see cref="P:System.Data.IDataRecord.FieldCount"/>. </exception><filterpriority>2</filterpriority>
		object IDataRecord.this[int i]
		{
			get
			{
				if (i >= 0 && _currentRow != null && _currentRow.Count > i)
					return string.IsNullOrEmpty(_currentRow[i]);
				throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
			}
		}

		/// <summary>
		/// Gets the column with the specified name.
		/// </summary>
		/// <returns>
		/// The column with the specified name as an <see cref="T:System.Object"/>.
		/// </returns>
		/// <param name="name">The name of the column to find. </param><exception cref="T:System.IndexOutOfRangeException">No column with the specified name was found. </exception><filterpriority>2</filterpriority>
		object IDataRecord.this[string name]
		{
			get
			{
				var i = GetOrdinal(name);
				if (i >= 0 && _currentRow != null && _currentRow.Count > i)
					return string.IsNullOrEmpty(_currentRow[i]);
				throw new Exception(string.Format("No column for index position {0} on line {1}", i, _lineCount));
			}
		}

		/// <summary>
		/// Closes the <see cref="T:System.Data.IDataReader"/> Object.
		/// </summary>
		/// <filterpriority>2</filterpriority>
		public void Close()
		{
			Dispose();
		}

		/// <summary>
		/// Returns a <see cref="T:System.Data.DataTable"/> that describes the column metadata of the <see cref="T:System.Data.IDataReader"/>.
		/// </summary>
		/// <returns>
		/// A <see cref="T:System.Data.DataTable"/> that describes the column metadata.
		/// </returns>
		/// <exception cref="T:System.InvalidOperationException">The <see cref="T:System.Data.IDataReader"/> is closed. </exception><filterpriority>2</filterpriority>
		public DataTable GetSchemaTable()
		{
			throw new NotImplementedException();
		}

		/// <summary>
		/// Advances the data reader to the next result, when reading the results of batch SQL statements.  
		/// </summary>
		/// <returns>
		/// always returns false.
		/// </returns>
		public bool NextResult()
		{
			return false;
		}

		/// <summary>
		/// Reads the next record
		/// </summary>
		/// <returns>True if a record was successfuly read, otherwise false</returns>
		public bool Read()
		{
			_currentRow = new List<string>();
			string line = _streamReader.ReadLine();

			if (line == null)
			{
				closeStreams();
				return false;
			}

			_lineCount++;
			parseLine(line);
			return true;
		}

		/// <summary>
		/// Gets a value indicating the depth of nesting for the current row.
		/// </summary>
		/// <returns>
		/// always returns 0
		/// </returns>
		public int Depth { get; private set; }

		/// <summary>
		/// Returns false if filestream is null
		/// </summary>
		public bool IsClosed
		{
			get { return _fileStream == null; }
		}

		/// <summary>
		/// Always returns 0
		/// </summary>
		public int RecordsAffected { get; private set; }

		#endregion IDataReader

		/// <summary>
		/// Gets or sets whether column values should be trimmed
		/// </summary>
		public bool TrimColumns { get; set; }

		/// <summary>
		/// Gets or sets whether the csv file has a header row
		/// </summary>
		public bool HasHeaderRow { get; set; }

		/// <summary>
		/// Returns a collection of field values or null if no record has been read
		/// </summary>
		public List<string> Values {
			get { return _currentRow; }
		}

		/// <summary>
		/// Returns a collection of column names
		/// </summary>
		public List<string> Names { get; set; }
	}
}
