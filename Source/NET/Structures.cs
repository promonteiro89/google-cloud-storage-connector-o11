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

} // OutSystems.NssGoogleCloudStorage_ext
