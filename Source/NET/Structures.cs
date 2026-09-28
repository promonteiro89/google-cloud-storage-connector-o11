using System;
using System.Collections;
using System.Data;
using System.Reflection;
using System.Runtime.Serialization;
using OutSystems.ObjectKeys;
using OutSystems.RuntimeCommon;
using OutSystems.HubEdition.RuntimePlatform;
using OutSystems.HubEdition.RuntimePlatform.Db;
using OutSystems.Internal.Db;

namespace OutSystems.NssGoogleCloudStorage_ext {

	/// <summary>
	/// Structure <code>STGCS_ObjectStructure</code> that represents the Service Studio structure
	///  <code>GCS_Object</code> <p> Description: Represents a file stored in Google Cloud Storage
	/// , including its name, size, and metadata.</p>
	/// </summary>
	[Serializable()]
	public partial struct STGCS_ObjectStructure: ISerializable, ITypedRecord<STGCS_ObjectStructure>, ISimpleRecord {
		internal static readonly GlobalObjectKey IdName = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*rhDw3MK0o0uM1CH11Ii2Ew");
		internal static readonly GlobalObjectKey IdSize = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*IjNIykOia0iVfjLnvmQlmg");
		internal static readonly GlobalObjectKey IdContentType = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*eEaCHYjEhEOj9TZrrVxTjQ");
		internal static readonly GlobalObjectKey IdUpdated = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*Lxxe8RcVeUqwDhPrfEN7TA");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("Name")]
		public string ssName;

		[System.Xml.Serialization.XmlElement("Size")]
		public long ssSize;

		[System.Xml.Serialization.XmlElement("ContentType")]
		public string ssContentType;

		[System.Xml.Serialization.XmlElement("Updated")]
		public DateTime ssUpdated;


		public BitArray OptimizedAttributes;

		public STGCS_ObjectStructure(params string[] dummy) {
			OptimizedAttributes = null;
			ssName = "";
			ssSize = 0L;
			ssContentType = "";
			ssUpdated = new DateTime(1900, 1, 1, 0, 0, 0);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[0];
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
				}
			}
			get {
				BitArray[] all = new BitArray[0];
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssName = r.ReadText(index++, "GCS_Object.Name", "");
			ssSize = r.ReadLongInteger(index++, "GCS_Object.Size", 0L);
			ssContentType = r.ReadText(index++, "GCS_Object.ContentType", "");
			ssUpdated = r.ReadDateTime(index++, "GCS_Object.Updated", new DateTime(1900, 1, 1, 0, 0, 0));
		}
		/// <summary>
		/// Read from database
		/// </summary>
		/// <param name="r"> Data reader</param>
		public void ReadDB(IDataReader r) {
			int index = 0;
			Read(r, ref index);
		}

		/// <summary>
		/// Read from record
		/// </summary>
		/// <param name="r"> Record</param>
		public void ReadIM(STGCS_ObjectStructure r) {
			this = r;
		}


		public static bool operator == (STGCS_ObjectStructure a, STGCS_ObjectStructure b) {
			if (a.ssName != b.ssName) return false;
			if (a.ssSize != b.ssSize) return false;
			if (a.ssContentType != b.ssContentType) return false;
			if (a.ssUpdated != b.ssUpdated) return false;
			return true;
		}

		public static bool operator != (STGCS_ObjectStructure a, STGCS_ObjectStructure b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(STGCS_ObjectStructure)) return false;
			return (this == (STGCS_ObjectStructure) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssName.GetHashCode()
				^ ssSize.GetHashCode()
				^ ssContentType.GetHashCode()
				^ ssUpdated.GetHashCode()
				;
			} catch {
				return base.GetHashCode();
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type objInfo = this.GetType();
			FieldInfo[] fields;
			fields = objInfo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			if (fields[i] .FieldType.IsSerializable)
			info.AddValue(fields[i] .Name, fields[i] .GetValue(this));
		}

		public STGCS_ObjectStructure(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssName = "";
			ssSize = 0L;
			ssContentType = "";
			ssUpdated = new DateTime(1900, 1, 1, 0, 0, 0);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssName", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssName' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssName = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssSize", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSize' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSize = (long) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssContentType", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssContentType' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssContentType = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssUpdated", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssUpdated' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssUpdated = (DateTime) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
		}

		public void InternalRecursiveSave() {
		}


		public STGCS_ObjectStructure Duplicate() {
			STGCS_ObjectStructure t;
			t.ssName = this.ssName;
			t.ssSize = this.ssSize;
			t.ssContentType = this.ssContentType;
			t.ssUpdated = this.ssUpdated;
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Structure");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
				fieldName = fieldName.ToLowerInvariant();
			}
			if (detailLevel > 0) {
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Name")) VarValue.AppendAttribute(recordElem, "Name", ssName, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Name");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Size")) VarValue.AppendAttribute(recordElem, "Size", ssSize, detailLevel, TypeKind.LongInteger); else VarValue.AppendOptimizedAttribute(recordElem, "Size");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ContentType")) VarValue.AppendAttribute(recordElem, "ContentType", ssContentType, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ContentType");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Updated")) VarValue.AppendAttribute(recordElem, "Updated", ssUpdated, detailLevel, TypeKind.DateTime); else VarValue.AppendOptimizedAttribute(recordElem, "Updated");
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "name") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Name")) variable.Value = ssName; else variable.Optimized = true;
			} else if (head == "size") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Size")) variable.Value = ssSize; else variable.Optimized = true;
			} else if (head == "contenttype") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ContentType")) variable.Value = ssContentType; else variable.Optimized = true;
			} else if (head == "updated") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Updated")) variable.Value = ssUpdated; else variable.Optimized = true;
			}
			if (variable.Found && tail != null) variable.EvaluateFields(this, head, tail);
		}

		public bool ChangedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public bool OptimizedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public object AttributeGet(GlobalObjectKey key) {
			if (key == IdName) {
				return ssName;
			} else if (key == IdSize) {
				return ssSize;
			} else if (key == IdContentType) {
				return ssContentType;
			} else if (key == IdUpdated) {
				return ssUpdated;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssName = (string) other.AttributeGet(IdName);
			ssSize = (long) other.AttributeGet(IdSize);
			ssContentType = (string) other.AttributeGet(IdContentType);
			ssUpdated = (DateTime) other.AttributeGet(IdUpdated);
		}
		public bool IsDefault() {
			STGCS_ObjectStructure defaultStruct = new STGCS_ObjectStructure(null);
			if (this.ssName != defaultStruct.ssName) return false;
			if (this.ssSize != defaultStruct.ssSize) return false;
			if (this.ssContentType != defaultStruct.ssContentType) return false;
			if (this.ssUpdated != defaultStruct.ssUpdated) return false;
			return true;
		}
	} // STGCS_ObjectStructure

	/// <summary>
	/// Structure <code>STGCS_BucketStructure</code> that represents the Service Studio structure
	///  <code>GCS_Bucket</code> <p> Description: Represents a Google Cloud Storage container, including it
	/// s location and storage class.</p>
	/// </summary>
	[Serializable()]
	public partial struct STGCS_BucketStructure: ISerializable, ITypedRecord<STGCS_BucketStructure>, ISimpleRecord {
		internal static readonly GlobalObjectKey IdName = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*tiqDuJNrPEKPCUiq6fIRDg");
		internal static readonly GlobalObjectKey IdLocation = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*OQMzoTURSkSJHY2tng0UrA");
		internal static readonly GlobalObjectKey IdStorageClass = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*ohOakQe_R0WvZrsQlEg5IQ");
		internal static readonly GlobalObjectKey IdCreated = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*mS5sZNRF40+2nFNRtKCQQQ");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("Name")]
		public string ssName;

		[System.Xml.Serialization.XmlElement("Location")]
		public string ssLocation;

		[System.Xml.Serialization.XmlElement("StorageClass")]
		public string ssStorageClass;

		[System.Xml.Serialization.XmlElement("Created")]
		public DateTime ssCreated;


		public BitArray OptimizedAttributes;

		public STGCS_BucketStructure(params string[] dummy) {
			OptimizedAttributes = null;
			ssName = "";
			ssLocation = "";
			ssStorageClass = "";
			ssCreated = new DateTime(1900, 1, 1, 0, 0, 0);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[0];
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
				}
			}
			get {
				BitArray[] all = new BitArray[0];
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssName = r.ReadText(index++, "GCS_Bucket.Name", "");
			ssLocation = r.ReadText(index++, "GCS_Bucket.Location", "");
			ssStorageClass = r.ReadText(index++, "GCS_Bucket.StorageClass", "");
			ssCreated = r.ReadDateTime(index++, "GCS_Bucket.Created", new DateTime(1900, 1, 1, 0, 0, 0));
		}
		/// <summary>
		/// Read from database
		/// </summary>
		/// <param name="r"> Data reader</param>
		public void ReadDB(IDataReader r) {
			int index = 0;
			Read(r, ref index);
		}

		/// <summary>
		/// Read from record
		/// </summary>
		/// <param name="r"> Record</param>
		public void ReadIM(STGCS_BucketStructure r) {
			this = r;
		}


		public static bool operator == (STGCS_BucketStructure a, STGCS_BucketStructure b) {
			if (a.ssName != b.ssName) return false;
			if (a.ssLocation != b.ssLocation) return false;
			if (a.ssStorageClass != b.ssStorageClass) return false;
			if (a.ssCreated != b.ssCreated) return false;
			return true;
		}

		public static bool operator != (STGCS_BucketStructure a, STGCS_BucketStructure b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(STGCS_BucketStructure)) return false;
			return (this == (STGCS_BucketStructure) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssName.GetHashCode()
				^ ssLocation.GetHashCode()
				^ ssStorageClass.GetHashCode()
				^ ssCreated.GetHashCode()
				;
			} catch {
				return base.GetHashCode();
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type objInfo = this.GetType();
			FieldInfo[] fields;
			fields = objInfo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			if (fields[i] .FieldType.IsSerializable)
			info.AddValue(fields[i] .Name, fields[i] .GetValue(this));
		}

		public STGCS_BucketStructure(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssName = "";
			ssLocation = "";
			ssStorageClass = "";
			ssCreated = new DateTime(1900, 1, 1, 0, 0, 0);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssName", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssName' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssName = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssLocation", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssLocation' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssLocation = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssStorageClass", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssStorageClass' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssStorageClass = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssCreated", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssCreated' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssCreated = (DateTime) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
		}

		public void InternalRecursiveSave() {
		}


		public STGCS_BucketStructure Duplicate() {
			STGCS_BucketStructure t;
			t.ssName = this.ssName;
			t.ssLocation = this.ssLocation;
			t.ssStorageClass = this.ssStorageClass;
			t.ssCreated = this.ssCreated;
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Structure");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
				fieldName = fieldName.ToLowerInvariant();
			}
			if (detailLevel > 0) {
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Name")) VarValue.AppendAttribute(recordElem, "Name", ssName, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Name");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Location")) VarValue.AppendAttribute(recordElem, "Location", ssLocation, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Location");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".StorageClass")) VarValue.AppendAttribute(recordElem, "StorageClass", ssStorageClass, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "StorageClass");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Created")) VarValue.AppendAttribute(recordElem, "Created", ssCreated, detailLevel, TypeKind.DateTime); else VarValue.AppendOptimizedAttribute(recordElem, "Created");
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "name") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Name")) variable.Value = ssName; else variable.Optimized = true;
			} else if (head == "location") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Location")) variable.Value = ssLocation; else variable.Optimized = true;
			} else if (head == "storageclass") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".StorageClass")) variable.Value = ssStorageClass; else variable.Optimized = true;
			} else if (head == "created") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Created")) variable.Value = ssCreated; else variable.Optimized = true;
			}
			if (variable.Found && tail != null) variable.EvaluateFields(this, head, tail);
		}

		public bool ChangedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public bool OptimizedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public object AttributeGet(GlobalObjectKey key) {
			if (key == IdName) {
				return ssName;
			} else if (key == IdLocation) {
				return ssLocation;
			} else if (key == IdStorageClass) {
				return ssStorageClass;
			} else if (key == IdCreated) {
				return ssCreated;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssName = (string) other.AttributeGet(IdName);
			ssLocation = (string) other.AttributeGet(IdLocation);
			ssStorageClass = (string) other.AttributeGet(IdStorageClass);
			ssCreated = (DateTime) other.AttributeGet(IdCreated);
		}
		public bool IsDefault() {
			STGCS_BucketStructure defaultStruct = new STGCS_BucketStructure(null);
			if (this.ssName != defaultStruct.ssName) return false;
			if (this.ssLocation != defaultStruct.ssLocation) return false;
			if (this.ssStorageClass != defaultStruct.ssStorageClass) return false;
			if (this.ssCreated != defaultStruct.ssCreated) return false;
			return true;
		}
	} // STGCS_BucketStructure

	/// <summary>
	/// Structure <code>STGCS_ObjectMetadataStructure</code> that represents the Service Studio structure
	///  <code>GCS_ObjectMetadata</code> <p> Description: Represents the full metadata of a file stored i
	/// n Google Cloud Storage, including its size, content type, integrity hashes (MD5/CRC32c), version
	///  identifiers (generation/metageneration), storage class, and creation/update timestamps. Retrieve
	/// d without downloading the object's content.</p>
	/// </summary>
	[Serializable()]
	public partial struct STGCS_ObjectMetadataStructure: ISerializable, ITypedRecord<STGCS_ObjectMetadataStructure>, ISimpleRecord {
		internal static readonly GlobalObjectKey IdName = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*evXImbJyxUKn5U5_tx0O7w");
		internal static readonly GlobalObjectKey IdBucket = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*2VsmGcUEFUWn4lzUYaJd7w");
		internal static readonly GlobalObjectKey IdSize = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*NM_1Wwkc_Uakf3IcEH6N7A");
		internal static readonly GlobalObjectKey IdContentType = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*pSTurulVuU+GKZSO5MwoRA");
		internal static readonly GlobalObjectKey IdContentEncoding = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*nrpPzAQl6kmftM_VabNgcA");
		internal static readonly GlobalObjectKey IdContentDisposition = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*ViIkJ208NEumI4LCEgXRHw");
		internal static readonly GlobalObjectKey IdCacheControl = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*_kEtB608KkuUxvMrEUtm_g");
		internal static readonly GlobalObjectKey IdMD5Hash = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*5+Y9RpLSqES0uZ6C_RkqSA");
		internal static readonly GlobalObjectKey IdCrc32c = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*o0bEO_q4pUSHSHaZWs7Hww");
		internal static readonly GlobalObjectKey IdETag = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*NhLiHLWdV0mW_DkaW7iwuA");
		internal static readonly GlobalObjectKey IdGeneration = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*uCXuuUymYU2t5i1kN5HD7Q");
		internal static readonly GlobalObjectKey IdMetageneration = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*SMOgXC+0Hkex0p8DupgD5Q");
		internal static readonly GlobalObjectKey IdStorageClass = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*eNgt4tc_jUOQaufXTruUXg");
		internal static readonly GlobalObjectKey IdMediaLink = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*LDV9_WwluUOmt1jOoTkGmQ");
		internal static readonly GlobalObjectKey IdTimeCreated = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*t58H2dqmqEqKNVXrOkh0eA");
		internal static readonly GlobalObjectKey IdUpdated = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*zXhtC9aDjEKD7hOmCdb2pg");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("Name")]
		public string ssName;

		[System.Xml.Serialization.XmlElement("Bucket")]
		public string ssBucket;

		[System.Xml.Serialization.XmlElement("Size")]
		public long ssSize;

		[System.Xml.Serialization.XmlElement("ContentType")]
		public string ssContentType;

		[System.Xml.Serialization.XmlElement("ContentEncoding")]
		public string ssContentEncoding;

		[System.Xml.Serialization.XmlElement("ContentDisposition")]
		public string ssContentDisposition;

		[System.Xml.Serialization.XmlElement("CacheControl")]
		public string ssCacheControl;

		[System.Xml.Serialization.XmlElement("MD5Hash")]
		public string ssMD5Hash;

		[System.Xml.Serialization.XmlElement("Crc32c")]
		public string ssCrc32c;

		[System.Xml.Serialization.XmlElement("ETag")]
		public string ssETag;

		[System.Xml.Serialization.XmlElement("Generation")]
		public long ssGeneration;

		[System.Xml.Serialization.XmlElement("Metageneration")]
		public long ssMetageneration;

		[System.Xml.Serialization.XmlElement("StorageClass")]
		public string ssStorageClass;

		[System.Xml.Serialization.XmlElement("MediaLink")]
		public string ssMediaLink;

		[System.Xml.Serialization.XmlElement("TimeCreated")]
		public DateTime ssTimeCreated;

		[System.Xml.Serialization.XmlElement("Updated")]
		public DateTime ssUpdated;


		public BitArray OptimizedAttributes;

		public STGCS_ObjectMetadataStructure(params string[] dummy) {
			OptimizedAttributes = null;
			ssName = "";
			ssBucket = "";
			ssSize = 0L;
			ssContentType = "";
			ssContentEncoding = "";
			ssContentDisposition = "";
			ssCacheControl = "";
			ssMD5Hash = "";
			ssCrc32c = "";
			ssETag = "";
			ssGeneration = 0L;
			ssMetageneration = 0L;
			ssStorageClass = "";
			ssMediaLink = "";
			ssTimeCreated = new DateTime(1900, 1, 1, 0, 0, 0);
			ssUpdated = new DateTime(1900, 1, 1, 0, 0, 0);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[0];
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
				}
			}
			get {
				BitArray[] all = new BitArray[0];
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssName = r.ReadText(index++, "GCS_ObjectMetadata.Name", "");
			ssBucket = r.ReadText(index++, "GCS_ObjectMetadata.Bucket", "");
			ssSize = r.ReadLongInteger(index++, "GCS_ObjectMetadata.Size", 0L);
			ssContentType = r.ReadText(index++, "GCS_ObjectMetadata.ContentType", "");
			ssContentEncoding = r.ReadText(index++, "GCS_ObjectMetadata.ContentEncoding", "");
			ssContentDisposition = r.ReadText(index++, "GCS_ObjectMetadata.ContentDisposition", "");
			ssCacheControl = r.ReadText(index++, "GCS_ObjectMetadata.CacheControl", "");
			ssMD5Hash = r.ReadText(index++, "GCS_ObjectMetadata.MD5Hash", "");
			ssCrc32c = r.ReadText(index++, "GCS_ObjectMetadata.Crc32c", "");
			ssETag = r.ReadText(index++, "GCS_ObjectMetadata.ETag", "");
			ssGeneration = r.ReadLongInteger(index++, "GCS_ObjectMetadata.Generation", 0L);
			ssMetageneration = r.ReadLongInteger(index++, "GCS_ObjectMetadata.Metageneration", 0L);
			ssStorageClass = r.ReadText(index++, "GCS_ObjectMetadata.StorageClass", "");
			ssMediaLink = r.ReadText(index++, "GCS_ObjectMetadata.MediaLink", "");
			ssTimeCreated = r.ReadDateTime(index++, "GCS_ObjectMetadata.TimeCreated", new DateTime(1900, 1, 1, 0, 0, 0));
			ssUpdated = r.ReadDateTime(index++, "GCS_ObjectMetadata.Updated", new DateTime(1900, 1, 1, 0, 0, 0));
		}
		/// <summary>
		/// Read from database
		/// </summary>
		/// <param name="r"> Data reader</param>
		public void ReadDB(IDataReader r) {
			int index = 0;
			Read(r, ref index);
		}

		/// <summary>
		/// Read from record
		/// </summary>
		/// <param name="r"> Record</param>
		public void ReadIM(STGCS_ObjectMetadataStructure r) {
			this = r;
		}


		public static bool operator == (STGCS_ObjectMetadataStructure a, STGCS_ObjectMetadataStructure b) {
			if (a.ssName != b.ssName) return false;
			if (a.ssBucket != b.ssBucket) return false;
			if (a.ssSize != b.ssSize) return false;
			if (a.ssContentType != b.ssContentType) return false;
			if (a.ssContentEncoding != b.ssContentEncoding) return false;
			if (a.ssContentDisposition != b.ssContentDisposition) return false;
			if (a.ssCacheControl != b.ssCacheControl) return false;
			if (a.ssMD5Hash != b.ssMD5Hash) return false;
			if (a.ssCrc32c != b.ssCrc32c) return false;
			if (a.ssETag != b.ssETag) return false;
			if (a.ssGeneration != b.ssGeneration) return false;
			if (a.ssMetageneration != b.ssMetageneration) return false;
			if (a.ssStorageClass != b.ssStorageClass) return false;
			if (a.ssMediaLink != b.ssMediaLink) return false;
			if (a.ssTimeCreated != b.ssTimeCreated) return false;
			if (a.ssUpdated != b.ssUpdated) return false;
			return true;
		}

		public static bool operator != (STGCS_ObjectMetadataStructure a, STGCS_ObjectMetadataStructure b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(STGCS_ObjectMetadataStructure)) return false;
			return (this == (STGCS_ObjectMetadataStructure) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssName.GetHashCode()
				^ ssBucket.GetHashCode()
				^ ssSize.GetHashCode()
				^ ssContentType.GetHashCode()
				^ ssContentEncoding.GetHashCode()
				^ ssContentDisposition.GetHashCode()
				^ ssCacheControl.GetHashCode()
				^ ssMD5Hash.GetHashCode()
				^ ssCrc32c.GetHashCode()
				^ ssETag.GetHashCode()
				^ ssGeneration.GetHashCode()
				^ ssMetageneration.GetHashCode()
				^ ssStorageClass.GetHashCode()
				^ ssMediaLink.GetHashCode()
				^ ssTimeCreated.GetHashCode()
				^ ssUpdated.GetHashCode()
				;
			} catch {
				return base.GetHashCode();
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type objInfo = this.GetType();
			FieldInfo[] fields;
			fields = objInfo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			if (fields[i] .FieldType.IsSerializable)
			info.AddValue(fields[i] .Name, fields[i] .GetValue(this));
		}

		public STGCS_ObjectMetadataStructure(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssName = "";
			ssBucket = "";
			ssSize = 0L;
			ssContentType = "";
			ssContentEncoding = "";
			ssContentDisposition = "";
			ssCacheControl = "";
			ssMD5Hash = "";
			ssCrc32c = "";
			ssETag = "";
			ssGeneration = 0L;
			ssMetageneration = 0L;
			ssStorageClass = "";
			ssMediaLink = "";
			ssTimeCreated = new DateTime(1900, 1, 1, 0, 0, 0);
			ssUpdated = new DateTime(1900, 1, 1, 0, 0, 0);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssName", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssName' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssName = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssBucket", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssBucket' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssBucket = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssSize", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSize' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSize = (long) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssContentType", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssContentType' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssContentType = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssContentEncoding", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssContentEncoding' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssContentEncoding = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssContentDisposition", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssContentDisposition' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssContentDisposition = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssCacheControl", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssCacheControl' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssCacheControl = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssMD5Hash", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssMD5Hash' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssMD5Hash = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssCrc32c", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssCrc32c' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssCrc32c = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssETag", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssETag' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssETag = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssGeneration", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssGeneration' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssGeneration = (long) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssMetageneration", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssMetageneration' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssMetageneration = (long) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssStorageClass", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssStorageClass' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssStorageClass = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssMediaLink", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssMediaLink' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssMediaLink = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssTimeCreated", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssTimeCreated' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssTimeCreated = (DateTime) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssUpdated", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssUpdated' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssUpdated = (DateTime) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
		}

		public void InternalRecursiveSave() {
		}


		public STGCS_ObjectMetadataStructure Duplicate() {
			STGCS_ObjectMetadataStructure t;
			t.ssName = this.ssName;
			t.ssBucket = this.ssBucket;
			t.ssSize = this.ssSize;
			t.ssContentType = this.ssContentType;
			t.ssContentEncoding = this.ssContentEncoding;
			t.ssContentDisposition = this.ssContentDisposition;
			t.ssCacheControl = this.ssCacheControl;
			t.ssMD5Hash = this.ssMD5Hash;
			t.ssCrc32c = this.ssCrc32c;
			t.ssETag = this.ssETag;
			t.ssGeneration = this.ssGeneration;
			t.ssMetageneration = this.ssMetageneration;
			t.ssStorageClass = this.ssStorageClass;
			t.ssMediaLink = this.ssMediaLink;
			t.ssTimeCreated = this.ssTimeCreated;
			t.ssUpdated = this.ssUpdated;
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Structure");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
				fieldName = fieldName.ToLowerInvariant();
			}
			if (detailLevel > 0) {
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Name")) VarValue.AppendAttribute(recordElem, "Name", ssName, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Name");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Bucket")) VarValue.AppendAttribute(recordElem, "Bucket", ssBucket, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Bucket");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Size")) VarValue.AppendAttribute(recordElem, "Size", ssSize, detailLevel, TypeKind.LongInteger); else VarValue.AppendOptimizedAttribute(recordElem, "Size");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ContentType")) VarValue.AppendAttribute(recordElem, "ContentType", ssContentType, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ContentType");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ContentEncoding")) VarValue.AppendAttribute(recordElem, "ContentEncoding", ssContentEncoding, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ContentEncoding");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ContentDisposition")) VarValue.AppendAttribute(recordElem, "ContentDisposition", ssContentDisposition, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ContentDisposition");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".CacheControl")) VarValue.AppendAttribute(recordElem, "CacheControl", ssCacheControl, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "CacheControl");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".MD5Hash")) VarValue.AppendAttribute(recordElem, "MD5Hash", ssMD5Hash, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "MD5Hash");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Crc32c")) VarValue.AppendAttribute(recordElem, "Crc32c", ssCrc32c, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Crc32c");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ETag")) VarValue.AppendAttribute(recordElem, "ETag", ssETag, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ETag");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Generation")) VarValue.AppendAttribute(recordElem, "Generation", ssGeneration, detailLevel, TypeKind.LongInteger); else VarValue.AppendOptimizedAttribute(recordElem, "Generation");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Metageneration")) VarValue.AppendAttribute(recordElem, "Metageneration", ssMetageneration, detailLevel, TypeKind.LongInteger); else VarValue.AppendOptimizedAttribute(recordElem, "Metageneration");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".StorageClass")) VarValue.AppendAttribute(recordElem, "StorageClass", ssStorageClass, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "StorageClass");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".MediaLink")) VarValue.AppendAttribute(recordElem, "MediaLink", ssMediaLink, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "MediaLink");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".TimeCreated")) VarValue.AppendAttribute(recordElem, "TimeCreated", ssTimeCreated, detailLevel, TypeKind.DateTime); else VarValue.AppendOptimizedAttribute(recordElem, "TimeCreated");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Updated")) VarValue.AppendAttribute(recordElem, "Updated", ssUpdated, detailLevel, TypeKind.DateTime); else VarValue.AppendOptimizedAttribute(recordElem, "Updated");
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "name") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Name")) variable.Value = ssName; else variable.Optimized = true;
			} else if (head == "bucket") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Bucket")) variable.Value = ssBucket; else variable.Optimized = true;
			} else if (head == "size") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Size")) variable.Value = ssSize; else variable.Optimized = true;
			} else if (head == "contenttype") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ContentType")) variable.Value = ssContentType; else variable.Optimized = true;
			} else if (head == "contentencoding") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ContentEncoding")) variable.Value = ssContentEncoding; else variable.Optimized = true;
			} else if (head == "contentdisposition") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ContentDisposition")) variable.Value = ssContentDisposition; else variable.Optimized = true;
			} else if (head == "cachecontrol") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".CacheControl")) variable.Value = ssCacheControl; else variable.Optimized = true;
			} else if (head == "md5hash") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".MD5Hash")) variable.Value = ssMD5Hash; else variable.Optimized = true;
			} else if (head == "crc32c") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Crc32c")) variable.Value = ssCrc32c; else variable.Optimized = true;
			} else if (head == "etag") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ETag")) variable.Value = ssETag; else variable.Optimized = true;
			} else if (head == "generation") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Generation")) variable.Value = ssGeneration; else variable.Optimized = true;
			} else if (head == "metageneration") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Metageneration")) variable.Value = ssMetageneration; else variable.Optimized = true;
			} else if (head == "storageclass") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".StorageClass")) variable.Value = ssStorageClass; else variable.Optimized = true;
			} else if (head == "medialink") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".MediaLink")) variable.Value = ssMediaLink; else variable.Optimized = true;
			} else if (head == "timecreated") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".TimeCreated")) variable.Value = ssTimeCreated; else variable.Optimized = true;
			} else if (head == "updated") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Updated")) variable.Value = ssUpdated; else variable.Optimized = true;
			}
			if (variable.Found && tail != null) variable.EvaluateFields(this, head, tail);
		}

		public bool ChangedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public bool OptimizedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public object AttributeGet(GlobalObjectKey key) {
			if (key == IdName) {
				return ssName;
			} else if (key == IdBucket) {
				return ssBucket;
			} else if (key == IdSize) {
				return ssSize;
			} else if (key == IdContentType) {
				return ssContentType;
			} else if (key == IdContentEncoding) {
				return ssContentEncoding;
			} else if (key == IdContentDisposition) {
				return ssContentDisposition;
			} else if (key == IdCacheControl) {
				return ssCacheControl;
			} else if (key == IdMD5Hash) {
				return ssMD5Hash;
			} else if (key == IdCrc32c) {
				return ssCrc32c;
			} else if (key == IdETag) {
				return ssETag;
			} else if (key == IdGeneration) {
				return ssGeneration;
			} else if (key == IdMetageneration) {
				return ssMetageneration;
			} else if (key == IdStorageClass) {
				return ssStorageClass;
			} else if (key == IdMediaLink) {
				return ssMediaLink;
			} else if (key == IdTimeCreated) {
				return ssTimeCreated;
			} else if (key == IdUpdated) {
				return ssUpdated;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssName = (string) other.AttributeGet(IdName);
			ssBucket = (string) other.AttributeGet(IdBucket);
			ssSize = (long) other.AttributeGet(IdSize);
			ssContentType = (string) other.AttributeGet(IdContentType);
			ssContentEncoding = (string) other.AttributeGet(IdContentEncoding);
			ssContentDisposition = (string) other.AttributeGet(IdContentDisposition);
			ssCacheControl = (string) other.AttributeGet(IdCacheControl);
			ssMD5Hash = (string) other.AttributeGet(IdMD5Hash);
			ssCrc32c = (string) other.AttributeGet(IdCrc32c);
			ssETag = (string) other.AttributeGet(IdETag);
			ssGeneration = (long) other.AttributeGet(IdGeneration);
			ssMetageneration = (long) other.AttributeGet(IdMetageneration);
			ssStorageClass = (string) other.AttributeGet(IdStorageClass);
			ssMediaLink = (string) other.AttributeGet(IdMediaLink);
			ssTimeCreated = (DateTime) other.AttributeGet(IdTimeCreated);
			ssUpdated = (DateTime) other.AttributeGet(IdUpdated);
		}
		public bool IsDefault() {
			STGCS_ObjectMetadataStructure defaultStruct = new STGCS_ObjectMetadataStructure(null);
			if (this.ssName != defaultStruct.ssName) return false;
			if (this.ssBucket != defaultStruct.ssBucket) return false;
			if (this.ssSize != defaultStruct.ssSize) return false;
			if (this.ssContentType != defaultStruct.ssContentType) return false;
			if (this.ssContentEncoding != defaultStruct.ssContentEncoding) return false;
			if (this.ssContentDisposition != defaultStruct.ssContentDisposition) return false;
			if (this.ssCacheControl != defaultStruct.ssCacheControl) return false;
			if (this.ssMD5Hash != defaultStruct.ssMD5Hash) return false;
			if (this.ssCrc32c != defaultStruct.ssCrc32c) return false;
			if (this.ssETag != defaultStruct.ssETag) return false;
			if (this.ssGeneration != defaultStruct.ssGeneration) return false;
			if (this.ssMetageneration != defaultStruct.ssMetageneration) return false;
			if (this.ssStorageClass != defaultStruct.ssStorageClass) return false;
			if (this.ssMediaLink != defaultStruct.ssMediaLink) return false;
			if (this.ssTimeCreated != defaultStruct.ssTimeCreated) return false;
			if (this.ssUpdated != defaultStruct.ssUpdated) return false;
			return true;
		}
	} // STGCS_ObjectMetadataStructure

	/// <summary>
	/// Structure <code>STGCS_PrefixStructure</code> that represents the Service Studio structure
	///  <code>GCS_Prefix</code> <p> Description: A folder-style entry returned by Object_List whe
	/// n Delimiter is set. Represents a group of objects that share a common prefix (e.g. 'images/2026/'),
	///  letting you navigate a bucket like a directory tree without listing every object inside.</p>
	/// </summary>
	[Serializable()]
	public partial struct STGCS_PrefixStructure: ISerializable, ITypedRecord<STGCS_PrefixStructure>, ISimpleRecord {
		internal static readonly GlobalObjectKey IdPrefix = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*ZgOyDe5mNUmeGlq3cpw3Nw");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("Prefix")]
		public string ssPrefix;


		public BitArray OptimizedAttributes;

		public STGCS_PrefixStructure(params string[] dummy) {
			OptimizedAttributes = null;
			ssPrefix = "";
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[0];
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
				}
			}
			get {
				BitArray[] all = new BitArray[0];
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssPrefix = r.ReadText(index++, "GCS_Prefix.Prefix", "");
		}
		/// <summary>
		/// Read from database
		/// </summary>
		/// <param name="r"> Data reader</param>
		public void ReadDB(IDataReader r) {
			int index = 0;
			Read(r, ref index);
		}

		/// <summary>
		/// Read from record
		/// </summary>
		/// <param name="r"> Record</param>
		public void ReadIM(STGCS_PrefixStructure r) {
			this = r;
		}


		public static bool operator == (STGCS_PrefixStructure a, STGCS_PrefixStructure b) {
			if (a.ssPrefix != b.ssPrefix) return false;
			return true;
		}

		public static bool operator != (STGCS_PrefixStructure a, STGCS_PrefixStructure b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(STGCS_PrefixStructure)) return false;
			return (this == (STGCS_PrefixStructure) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssPrefix.GetHashCode()
				;
			} catch {
				return base.GetHashCode();
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type objInfo = this.GetType();
			FieldInfo[] fields;
			fields = objInfo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			if (fields[i] .FieldType.IsSerializable)
			info.AddValue(fields[i] .Name, fields[i] .GetValue(this));
		}

		public STGCS_PrefixStructure(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssPrefix = "";
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssPrefix", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssPrefix' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssPrefix = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
		}

		public void InternalRecursiveSave() {
		}


		public STGCS_PrefixStructure Duplicate() {
			STGCS_PrefixStructure t;
			t.ssPrefix = this.ssPrefix;
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Structure");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
				fieldName = fieldName.ToLowerInvariant();
			}
			if (detailLevel > 0) {
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Prefix")) VarValue.AppendAttribute(recordElem, "Prefix", ssPrefix, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Prefix");
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "prefix") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Prefix")) variable.Value = ssPrefix; else variable.Optimized = true;
			}
			if (variable.Found && tail != null) variable.EvaluateFields(this, head, tail);
		}

		public bool ChangedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public bool OptimizedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public object AttributeGet(GlobalObjectKey key) {
			if (key == IdPrefix) {
				return ssPrefix;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssPrefix = (string) other.AttributeGet(IdPrefix);
		}
		public bool IsDefault() {
			STGCS_PrefixStructure defaultStruct = new STGCS_PrefixStructure(null);
			if (this.ssPrefix != defaultStruct.ssPrefix) return false;
			return true;
		}
	} // STGCS_PrefixStructure

	/// <summary>
	/// Structure <code>STGCS_MetadataEntryStructure</code> that represents the Service Studio structure
	///  <code>GCS_MetadataEntry</code> <p> Description: A single custom metadata key-value pair stored wit
	/// h an object. Used by Object_Upload, Object_UpdateMetadata, and returned by Object_GetMetadata.</p>
	/// </summary>
	[Serializable()]
	public partial struct STGCS_MetadataEntryStructure: ISerializable, ITypedRecord<STGCS_MetadataEntryStructure>, ISimpleRecord {
		internal static readonly GlobalObjectKey IdKey = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*YpuiIPEZlUyBvwYqEePjHA");
		internal static readonly GlobalObjectKey IdValue = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*nLmU4OtiAUeYDlxpkN6Bkg");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("Key")]
		public string ssKey;

		[System.Xml.Serialization.XmlElement("Value")]
		public string ssValue;


		public BitArray OptimizedAttributes;

		public STGCS_MetadataEntryStructure(params string[] dummy) {
			OptimizedAttributes = null;
			ssKey = "";
			ssValue = "";
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[0];
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
				}
			}
			get {
				BitArray[] all = new BitArray[0];
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssKey = r.ReadText(index++, "GCS_MetadataEntry.Key", "");
			ssValue = r.ReadText(index++, "GCS_MetadataEntry.Value", "");
		}
		/// <summary>
		/// Read from database
		/// </summary>
		/// <param name="r"> Data reader</param>
		public void ReadDB(IDataReader r) {
			int index = 0;
			Read(r, ref index);
		}

		/// <summary>
		/// Read from record
		/// </summary>
		/// <param name="r"> Record</param>
		public void ReadIM(STGCS_MetadataEntryStructure r) {
			this = r;
		}


		public static bool operator == (STGCS_MetadataEntryStructure a, STGCS_MetadataEntryStructure b) {
			if (a.ssKey != b.ssKey) return false;
			if (a.ssValue != b.ssValue) return false;
			return true;
		}

		public static bool operator != (STGCS_MetadataEntryStructure a, STGCS_MetadataEntryStructure b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(STGCS_MetadataEntryStructure)) return false;
			return (this == (STGCS_MetadataEntryStructure) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssKey.GetHashCode()
				^ ssValue.GetHashCode()
				;
			} catch {
				return base.GetHashCode();
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type objInfo = this.GetType();
			FieldInfo[] fields;
			fields = objInfo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			if (fields[i] .FieldType.IsSerializable)
			info.AddValue(fields[i] .Name, fields[i] .GetValue(this));
		}

		public STGCS_MetadataEntryStructure(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssKey = "";
			ssValue = "";
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssKey", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssKey' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssKey = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssValue", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssValue' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssValue = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
		}

		public void InternalRecursiveSave() {
		}


		public STGCS_MetadataEntryStructure Duplicate() {
			STGCS_MetadataEntryStructure t;
			t.ssKey = this.ssKey;
			t.ssValue = this.ssValue;
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Structure");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
				fieldName = fieldName.ToLowerInvariant();
			}
			if (detailLevel > 0) {
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Key")) VarValue.AppendAttribute(recordElem, "Key", ssKey, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Key");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Value")) VarValue.AppendAttribute(recordElem, "Value", ssValue, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Value");
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "key") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Key")) variable.Value = ssKey; else variable.Optimized = true;
			} else if (head == "value") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Value")) variable.Value = ssValue; else variable.Optimized = true;
			}
			if (variable.Found && tail != null) variable.EvaluateFields(this, head, tail);
		}

		public bool ChangedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public bool OptimizedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public object AttributeGet(GlobalObjectKey key) {
			if (key == IdKey) {
				return ssKey;
			} else if (key == IdValue) {
				return ssValue;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssKey = (string) other.AttributeGet(IdKey);
			ssValue = (string) other.AttributeGet(IdValue);
		}
		public bool IsDefault() {
			STGCS_MetadataEntryStructure defaultStruct = new STGCS_MetadataEntryStructure(null);
			if (this.ssKey != defaultStruct.ssKey) return false;
			if (this.ssValue != defaultStruct.ssValue) return false;
			return true;
		}
	} // STGCS_MetadataEntryStructure

	/// <summary>
	/// Structure <code>STGCS_AuthenticationStructure</code> that represents the Service Studio structure
	///  <code>GCS_Authentication</code> <p> Description: Google Cloud credentials. Two methods ar
	/// e supported: 'WorkloadIdentityFederation' (recommended, keyless) and 'ServiceAccountKey' (legacy:
	///  ClientEmail + PrivateKey). Leave AuthenticationMethod empty to use ServiceAccountKey.</p>
	/// </summary>
	[Serializable()]
	public partial struct STGCS_AuthenticationStructure: ISerializable, ITypedRecord<STGCS_AuthenticationStructure>, ISimpleRecord {
		internal static readonly GlobalObjectKey IdProjectId = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*pFA_kJAr00iNfSn7++O_pw");
		internal static readonly GlobalObjectKey IdAuthenticationMethod = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*P2OadTaoFEeu0eF98aYiuQ");
		internal static readonly GlobalObjectKey IdClientEmail = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*0SEUzqyOF0WMGNoI0uSEzA");
		internal static readonly GlobalObjectKey IdPrivateKey = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*ZVZjBAoEhUuPrnbN8Br9tg");
		internal static readonly GlobalObjectKey IdWorkloadIdentityProvider = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*RogHe4P+WUutc9qWTu98tA");
		internal static readonly GlobalObjectKey IdServiceAccountEmail = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*28SN1YcTxk6wxC13RSLdlA");
		internal static readonly GlobalObjectKey IdTokenEndpoint = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*qIIN86k7fE+M4LqGxyHL8A");
		internal static readonly GlobalObjectKey IdClientId = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*jpWVqo9KeEG1rwwlOL9eFw");
		internal static readonly GlobalObjectKey IdClientSecret = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*2g2bP_65pEmbxnWghkKu3A");
		internal static readonly GlobalObjectKey IdScope = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*Hxu9iaHxMkmv+3XRFfTwgg");
		internal static readonly GlobalObjectKey IdAudience = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*PDTZVirpqUaLJd6S+5C5hA");
		internal static readonly GlobalObjectKey IdSubjectToken = GlobalObjectKey.Parse("LSXUyDLU9EaMeZz89Pc82w*y+LSoXvlKUS5rOXUsJTiHA");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("ProjectId")]
		public string ssProjectId;

		[System.Xml.Serialization.XmlElement("AuthenticationMethod")]
		public string ssAuthenticationMethod;

		[System.Xml.Serialization.XmlElement("ClientEmail")]
		public string ssClientEmail;

		[System.Xml.Serialization.XmlElement("PrivateKey")]
		public string ssPrivateKey;

		[System.Xml.Serialization.XmlElement("WorkloadIdentityProvider")]
		public string ssWorkloadIdentityProvider;

		[System.Xml.Serialization.XmlElement("ServiceAccountEmail")]
		public string ssServiceAccountEmail;

		[System.Xml.Serialization.XmlElement("TokenEndpoint")]
		public string ssTokenEndpoint;

		[System.Xml.Serialization.XmlElement("ClientId")]
		public string ssClientId;

		[System.Xml.Serialization.XmlElement("ClientSecret")]
		public string ssClientSecret;

		[System.Xml.Serialization.XmlElement("Scope")]
		public string ssScope;

		[System.Xml.Serialization.XmlElement("Audience")]
		public string ssAudience;

		[System.Xml.Serialization.XmlElement("SubjectToken")]
		public string ssSubjectToken;


		public BitArray OptimizedAttributes;

		public STGCS_AuthenticationStructure(params string[] dummy) {
			OptimizedAttributes = null;
			ssProjectId = "";
			ssAuthenticationMethod = "ServiceAccountKey";
			ssClientEmail = "";
			ssPrivateKey = "";
			ssWorkloadIdentityProvider = "";
			ssServiceAccountEmail = "";
			ssTokenEndpoint = "";
			ssClientId = "";
			ssClientSecret = "";
			ssScope = "";
			ssAudience = "";
			ssSubjectToken = "";
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[0];
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
				}
			}
			get {
				BitArray[] all = new BitArray[0];
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssProjectId = r.ReadText(index++, "GCS_Authentication.ProjectId", "");
			ssAuthenticationMethod = r.ReadText(index++, "GCS_Authentication.AuthenticationMethod", "");
			ssClientEmail = r.ReadText(index++, "GCS_Authentication.ClientEmail", "");
			ssPrivateKey = r.ReadText(index++, "GCS_Authentication.PrivateKey", "");
			ssWorkloadIdentityProvider = r.ReadText(index++, "GCS_Authentication.WorkloadIdentityProvider", "");
			ssServiceAccountEmail = r.ReadText(index++, "GCS_Authentication.ServiceAccountEmail", "");
			ssTokenEndpoint = r.ReadText(index++, "GCS_Authentication.TokenEndpoint", "");
			ssClientId = r.ReadText(index++, "GCS_Authentication.ClientId", "");
			ssClientSecret = r.ReadText(index++, "GCS_Authentication.ClientSecret", "");
			ssScope = r.ReadText(index++, "GCS_Authentication.Scope", "");
			ssAudience = r.ReadText(index++, "GCS_Authentication.Audience", "");
			ssSubjectToken = r.ReadText(index++, "GCS_Authentication.SubjectToken", "");
		}
		/// <summary>
		/// Read from database
		/// </summary>
		/// <param name="r"> Data reader</param>
		public void ReadDB(IDataReader r) {
			int index = 0;
			Read(r, ref index);
		}

		/// <summary>
		/// Read from record
		/// </summary>
		/// <param name="r"> Record</param>
		public void ReadIM(STGCS_AuthenticationStructure r) {
			this = r;
		}


		public static bool operator == (STGCS_AuthenticationStructure a, STGCS_AuthenticationStructure b) {
			if (a.ssProjectId != b.ssProjectId) return false;
			if (a.ssAuthenticationMethod != b.ssAuthenticationMethod) return false;
			if (a.ssClientEmail != b.ssClientEmail) return false;
			if (a.ssPrivateKey != b.ssPrivateKey) return false;
			if (a.ssWorkloadIdentityProvider != b.ssWorkloadIdentityProvider) return false;
			if (a.ssServiceAccountEmail != b.ssServiceAccountEmail) return false;
			if (a.ssTokenEndpoint != b.ssTokenEndpoint) return false;
			if (a.ssClientId != b.ssClientId) return false;
			if (a.ssClientSecret != b.ssClientSecret) return false;
			if (a.ssScope != b.ssScope) return false;
			if (a.ssAudience != b.ssAudience) return false;
			if (a.ssSubjectToken != b.ssSubjectToken) return false;
			return true;
		}

		public static bool operator != (STGCS_AuthenticationStructure a, STGCS_AuthenticationStructure b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(STGCS_AuthenticationStructure)) return false;
			return (this == (STGCS_AuthenticationStructure) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssProjectId.GetHashCode()
				^ ssAuthenticationMethod.GetHashCode()
				^ ssClientEmail.GetHashCode()
				^ ssPrivateKey.GetHashCode()
				^ ssWorkloadIdentityProvider.GetHashCode()
				^ ssServiceAccountEmail.GetHashCode()
				^ ssTokenEndpoint.GetHashCode()
				^ ssClientId.GetHashCode()
				^ ssClientSecret.GetHashCode()
				^ ssScope.GetHashCode()
				^ ssAudience.GetHashCode()
				^ ssSubjectToken.GetHashCode()
				;
			} catch {
				return base.GetHashCode();
			}
		}

		public void GetObjectData(SerializationInfo info, StreamingContext context) {
			Type objInfo = this.GetType();
			FieldInfo[] fields;
			fields = objInfo.GetFields(BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			for (int i = 0; i < fields.Length; i++)
			if (fields[i] .FieldType.IsSerializable)
			info.AddValue(fields[i] .Name, fields[i] .GetValue(this));
		}

		public STGCS_AuthenticationStructure(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssProjectId = "";
			ssAuthenticationMethod = "ServiceAccountKey";
			ssClientEmail = "";
			ssPrivateKey = "";
			ssWorkloadIdentityProvider = "";
			ssServiceAccountEmail = "";
			ssTokenEndpoint = "";
			ssClientId = "";
			ssClientSecret = "";
			ssScope = "";
			ssAudience = "";
			ssSubjectToken = "";
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssProjectId", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssProjectId' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssProjectId = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssAuthenticationMethod", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssAuthenticationMethod' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssAuthenticationMethod = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssClientEmail", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssClientEmail' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssClientEmail = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssPrivateKey", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssPrivateKey' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssPrivateKey = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssWorkloadIdentityProvider", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssWorkloadIdentityProvider' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssWorkloadIdentityProvider = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssServiceAccountEmail", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssServiceAccountEmail' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssServiceAccountEmail = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssTokenEndpoint", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssTokenEndpoint' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssTokenEndpoint = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssClientId", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssClientId' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssClientId = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssClientSecret", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssClientSecret' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssClientSecret = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssScope", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssScope' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssScope = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssAudience", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssAudience' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssAudience = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
			fieldInfo = objInfo.GetField("ssSubjectToken", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSubjectToken' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSubjectToken = (string) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
		}

		public void InternalRecursiveSave() {
		}


		public STGCS_AuthenticationStructure Duplicate() {
			STGCS_AuthenticationStructure t;
			t.ssProjectId = this.ssProjectId;
			t.ssAuthenticationMethod = this.ssAuthenticationMethod;
			t.ssClientEmail = this.ssClientEmail;
			t.ssPrivateKey = this.ssPrivateKey;
			t.ssWorkloadIdentityProvider = this.ssWorkloadIdentityProvider;
			t.ssServiceAccountEmail = this.ssServiceAccountEmail;
			t.ssTokenEndpoint = this.ssTokenEndpoint;
			t.ssClientId = this.ssClientId;
			t.ssClientSecret = this.ssClientSecret;
			t.ssScope = this.ssScope;
			t.ssAudience = this.ssAudience;
			t.ssSubjectToken = this.ssSubjectToken;
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Structure");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
				fieldName = fieldName.ToLowerInvariant();
			}
			if (detailLevel > 0) {
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ProjectId")) VarValue.AppendAttribute(recordElem, "ProjectId", ssProjectId, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ProjectId");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".AuthenticationMethod")) VarValue.AppendAttribute(recordElem, "AuthenticationMethod", ssAuthenticationMethod, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "AuthenticationMethod");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ClientEmail")) VarValue.AppendAttribute(recordElem, "ClientEmail", ssClientEmail, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ClientEmail");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".PrivateKey")) VarValue.AppendAttribute(recordElem, "PrivateKey", ssPrivateKey, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "PrivateKey");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".WorkloadIdentityProvider")) VarValue.AppendAttribute(recordElem, "WorkloadIdentityProvider", ssWorkloadIdentityProvider, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "WorkloadIdentityProvider");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ServiceAccountEmail")) VarValue.AppendAttribute(recordElem, "ServiceAccountEmail", ssServiceAccountEmail, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ServiceAccountEmail");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".TokenEndpoint")) VarValue.AppendAttribute(recordElem, "TokenEndpoint", ssTokenEndpoint, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "TokenEndpoint");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ClientId")) VarValue.AppendAttribute(recordElem, "ClientId", ssClientId, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ClientId");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".ClientSecret")) VarValue.AppendAttribute(recordElem, "ClientSecret", ssClientSecret, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "ClientSecret");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Scope")) VarValue.AppendAttribute(recordElem, "Scope", ssScope, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Scope");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".Audience")) VarValue.AppendAttribute(recordElem, "Audience", ssAudience, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "Audience");
				if (!VarValue.FieldIsOptimized(parent, fieldName + ".SubjectToken")) VarValue.AppendAttribute(recordElem, "SubjectToken", ssSubjectToken, detailLevel, TypeKind.Text); else VarValue.AppendOptimizedAttribute(recordElem, "SubjectToken");
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "projectid") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ProjectId")) variable.Value = ssProjectId; else variable.Optimized = true;
			} else if (head == "authenticationmethod") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".AuthenticationMethod")) variable.Value = ssAuthenticationMethod; else variable.Optimized = true;
			} else if (head == "clientemail") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ClientEmail")) variable.Value = ssClientEmail; else variable.Optimized = true;
			} else if (head == "privatekey") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".PrivateKey")) variable.Value = ssPrivateKey; else variable.Optimized = true;
			} else if (head == "workloadidentityprovider") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".WorkloadIdentityProvider")) variable.Value = ssWorkloadIdentityProvider; else variable.Optimized = true;
			} else if (head == "serviceaccountemail") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ServiceAccountEmail")) variable.Value = ssServiceAccountEmail; else variable.Optimized = true;
			} else if (head == "tokenendpoint") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".TokenEndpoint")) variable.Value = ssTokenEndpoint; else variable.Optimized = true;
			} else if (head == "clientid") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ClientId")) variable.Value = ssClientId; else variable.Optimized = true;
			} else if (head == "clientsecret") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".ClientSecret")) variable.Value = ssClientSecret; else variable.Optimized = true;
			} else if (head == "scope") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Scope")) variable.Value = ssScope; else variable.Optimized = true;
			} else if (head == "audience") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".Audience")) variable.Value = ssAudience; else variable.Optimized = true;
			} else if (head == "subjecttoken") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".SubjectToken")) variable.Value = ssSubjectToken; else variable.Optimized = true;
			}
			if (variable.Found && tail != null) variable.EvaluateFields(this, head, tail);
		}

		public bool ChangedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public bool OptimizedAttributeGet(GlobalObjectKey key) {
			throw new Exception("Method not Supported");
		}

		public object AttributeGet(GlobalObjectKey key) {
			if (key == IdProjectId) {
				return ssProjectId;
			} else if (key == IdAuthenticationMethod) {
				return ssAuthenticationMethod;
			} else if (key == IdClientEmail) {
				return ssClientEmail;
			} else if (key == IdPrivateKey) {
				return ssPrivateKey;
			} else if (key == IdWorkloadIdentityProvider) {
				return ssWorkloadIdentityProvider;
			} else if (key == IdServiceAccountEmail) {
				return ssServiceAccountEmail;
			} else if (key == IdTokenEndpoint) {
				return ssTokenEndpoint;
			} else if (key == IdClientId) {
				return ssClientId;
			} else if (key == IdClientSecret) {
				return ssClientSecret;
			} else if (key == IdScope) {
				return ssScope;
			} else if (key == IdAudience) {
				return ssAudience;
			} else if (key == IdSubjectToken) {
				return ssSubjectToken;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssProjectId = (string) other.AttributeGet(IdProjectId);
			ssAuthenticationMethod = (string) other.AttributeGet(IdAuthenticationMethod);
			ssClientEmail = (string) other.AttributeGet(IdClientEmail);
			ssPrivateKey = (string) other.AttributeGet(IdPrivateKey);
			ssWorkloadIdentityProvider = (string) other.AttributeGet(IdWorkloadIdentityProvider);
			ssServiceAccountEmail = (string) other.AttributeGet(IdServiceAccountEmail);
			ssTokenEndpoint = (string) other.AttributeGet(IdTokenEndpoint);
			ssClientId = (string) other.AttributeGet(IdClientId);
			ssClientSecret = (string) other.AttributeGet(IdClientSecret);
			ssScope = (string) other.AttributeGet(IdScope);
			ssAudience = (string) other.AttributeGet(IdAudience);
			ssSubjectToken = (string) other.AttributeGet(IdSubjectToken);
		}
		public bool IsDefault() {
			STGCS_AuthenticationStructure defaultStruct = new STGCS_AuthenticationStructure(null);
			if (this.ssProjectId != defaultStruct.ssProjectId) return false;
			if (this.ssAuthenticationMethod != defaultStruct.ssAuthenticationMethod) return false;
			if (this.ssClientEmail != defaultStruct.ssClientEmail) return false;
			if (this.ssPrivateKey != defaultStruct.ssPrivateKey) return false;
			if (this.ssWorkloadIdentityProvider != defaultStruct.ssWorkloadIdentityProvider) return false;
			if (this.ssServiceAccountEmail != defaultStruct.ssServiceAccountEmail) return false;
			if (this.ssTokenEndpoint != defaultStruct.ssTokenEndpoint) return false;
			if (this.ssClientId != defaultStruct.ssClientId) return false;
			if (this.ssClientSecret != defaultStruct.ssClientSecret) return false;
			if (this.ssScope != defaultStruct.ssScope) return false;
			if (this.ssAudience != defaultStruct.ssAudience) return false;
			if (this.ssSubjectToken != defaultStruct.ssSubjectToken) return false;
			return true;
		}
	} // STGCS_AuthenticationStructure

} // OutSystems.NssGoogleCloudStorage_ext
