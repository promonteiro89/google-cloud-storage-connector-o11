using System;
using System.Collections;
using System.Data;
using System.Runtime.Serialization;
using System.Reflection;
using System.Xml;
using OutSystems.ObjectKeys;
using OutSystems.RuntimeCommon;
using OutSystems.HubEdition.RuntimePlatform;
using OutSystems.HubEdition.RuntimePlatform.Db;
using OutSystems.Internal.Db;

namespace OutSystems.NssGoogleCloudStorage_ext {

	/// <summary>
	/// Structure <code>RCGCS_ObjectRecord</code>
	/// </summary>
	[Serializable()]
	public partial struct RCGCS_ObjectRecord: ISerializable, ITypedRecord<RCGCS_ObjectRecord> {
		internal static readonly GlobalObjectKey IdGCS_Object = GlobalObjectKey.Parse("2UmDmepsh0WSfJ_D1JexCA*b_xCV5OYuqgX2m71lYVPcQ");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("GCS_Object")]
		public STGCS_ObjectStructure ssSTGCS_Object;


		public static implicit operator STGCS_ObjectStructure(RCGCS_ObjectRecord r) {
			return r.ssSTGCS_Object;
		}

		public static implicit operator RCGCS_ObjectRecord(STGCS_ObjectStructure r) {
			RCGCS_ObjectRecord res = new RCGCS_ObjectRecord(null);
			res.ssSTGCS_Object = r;
			return res;
		}

		public BitArray OptimizedAttributes;

		public RCGCS_ObjectRecord(params string[] dummy) {
			OptimizedAttributes = null;
			ssSTGCS_Object = new STGCS_ObjectStructure(null);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[1];
			all[0] = null;
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
					ssSTGCS_Object.OptimizedAttributes = value[0];
				}
			}
			get {
				BitArray[] all = new BitArray[1];
				all[0] = null;
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssSTGCS_Object.Read(r, ref index);
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
		public void ReadIM(RCGCS_ObjectRecord r) {
			this = r;
		}


		public static bool operator == (RCGCS_ObjectRecord a, RCGCS_ObjectRecord b) {
			if (a.ssSTGCS_Object != b.ssSTGCS_Object) return false;
			return true;
		}

		public static bool operator != (RCGCS_ObjectRecord a, RCGCS_ObjectRecord b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(RCGCS_ObjectRecord)) return false;
			return (this == (RCGCS_ObjectRecord) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssSTGCS_Object.GetHashCode()
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

		public RCGCS_ObjectRecord(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssSTGCS_Object = new STGCS_ObjectStructure(null);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssSTGCS_Object", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSTGCS_Object' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSTGCS_Object = (STGCS_ObjectStructure) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
			ssSTGCS_Object.RecursiveReset();
		}

		public void InternalRecursiveSave() {
			ssSTGCS_Object.InternalRecursiveSave();
		}


		public RCGCS_ObjectRecord Duplicate() {
			RCGCS_ObjectRecord t;
			t.ssSTGCS_Object = (STGCS_ObjectStructure) this.ssSTGCS_Object.Duplicate();
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Record");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
			}
			if (detailLevel > 0) {
				ssSTGCS_Object.ToXml(this, recordElem, "GCS_Object", detailLevel - 1);
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "gcs_object") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".GCS_Object")) variable.Value = ssSTGCS_Object; else variable.Optimized = true;
				variable.SetFieldName("gcs_object");
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
			if (key == IdGCS_Object) {
				return ssSTGCS_Object;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssSTGCS_Object.FillFromOther((IRecord) other.AttributeGet(IdGCS_Object));
		}
		public bool IsDefault() {
			RCGCS_ObjectRecord defaultStruct = new RCGCS_ObjectRecord(null);
			if (this.ssSTGCS_Object != defaultStruct.ssSTGCS_Object) return false;
			return true;
		}
	} // RCGCS_ObjectRecord

	/// <summary>
	/// Structure <code>RCGCS_BucketRecord</code>
	/// </summary>
	[Serializable()]
	public partial struct RCGCS_BucketRecord: ISerializable, ITypedRecord<RCGCS_BucketRecord> {
		internal static readonly GlobalObjectKey IdGCS_Bucket = GlobalObjectKey.Parse("2UmDmepsh0WSfJ_D1JexCA*8_IAqzgAf04mbXj4qh7OcQ");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("GCS_Bucket")]
		public STGCS_BucketStructure ssSTGCS_Bucket;


		public static implicit operator STGCS_BucketStructure(RCGCS_BucketRecord r) {
			return r.ssSTGCS_Bucket;
		}

		public static implicit operator RCGCS_BucketRecord(STGCS_BucketStructure r) {
			RCGCS_BucketRecord res = new RCGCS_BucketRecord(null);
			res.ssSTGCS_Bucket = r;
			return res;
		}

		public BitArray OptimizedAttributes;

		public RCGCS_BucketRecord(params string[] dummy) {
			OptimizedAttributes = null;
			ssSTGCS_Bucket = new STGCS_BucketStructure(null);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[1];
			all[0] = null;
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
					ssSTGCS_Bucket.OptimizedAttributes = value[0];
				}
			}
			get {
				BitArray[] all = new BitArray[1];
				all[0] = null;
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssSTGCS_Bucket.Read(r, ref index);
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
		public void ReadIM(RCGCS_BucketRecord r) {
			this = r;
		}


		public static bool operator == (RCGCS_BucketRecord a, RCGCS_BucketRecord b) {
			if (a.ssSTGCS_Bucket != b.ssSTGCS_Bucket) return false;
			return true;
		}

		public static bool operator != (RCGCS_BucketRecord a, RCGCS_BucketRecord b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(RCGCS_BucketRecord)) return false;
			return (this == (RCGCS_BucketRecord) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssSTGCS_Bucket.GetHashCode()
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

		public RCGCS_BucketRecord(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssSTGCS_Bucket = new STGCS_BucketStructure(null);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssSTGCS_Bucket", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSTGCS_Bucket' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSTGCS_Bucket = (STGCS_BucketStructure) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
			ssSTGCS_Bucket.RecursiveReset();
		}

		public void InternalRecursiveSave() {
			ssSTGCS_Bucket.InternalRecursiveSave();
		}


		public RCGCS_BucketRecord Duplicate() {
			RCGCS_BucketRecord t;
			t.ssSTGCS_Bucket = (STGCS_BucketStructure) this.ssSTGCS_Bucket.Duplicate();
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Record");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
			}
			if (detailLevel > 0) {
				ssSTGCS_Bucket.ToXml(this, recordElem, "GCS_Bucket", detailLevel - 1);
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "gcs_bucket") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".GCS_Bucket")) variable.Value = ssSTGCS_Bucket; else variable.Optimized = true;
				variable.SetFieldName("gcs_bucket");
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
			if (key == IdGCS_Bucket) {
				return ssSTGCS_Bucket;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssSTGCS_Bucket.FillFromOther((IRecord) other.AttributeGet(IdGCS_Bucket));
		}
		public bool IsDefault() {
			RCGCS_BucketRecord defaultStruct = new RCGCS_BucketRecord(null);
			if (this.ssSTGCS_Bucket != defaultStruct.ssSTGCS_Bucket) return false;
			return true;
		}
	} // RCGCS_BucketRecord

	/// <summary>
	/// Structure <code>RCGCS_ObjectMetadataRecord</code>
	/// </summary>
	[Serializable()]
	public partial struct RCGCS_ObjectMetadataRecord: ISerializable, ITypedRecord<RCGCS_ObjectMetadataRecord> {
		internal static readonly GlobalObjectKey IdGCS_ObjectMetadata = GlobalObjectKey.Parse("2UmDmepsh0WSfJ_D1JexCA*YheYWLaD1WyXj0BW6C_2mw");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("GCS_ObjectMetadata")]
		public STGCS_ObjectMetadataStructure ssSTGCS_ObjectMetadata;


		public static implicit operator STGCS_ObjectMetadataStructure(RCGCS_ObjectMetadataRecord r) {
			return r.ssSTGCS_ObjectMetadata;
		}

		public static implicit operator RCGCS_ObjectMetadataRecord(STGCS_ObjectMetadataStructure r) {
			RCGCS_ObjectMetadataRecord res = new RCGCS_ObjectMetadataRecord(null);
			res.ssSTGCS_ObjectMetadata = r;
			return res;
		}

		public BitArray OptimizedAttributes;

		public RCGCS_ObjectMetadataRecord(params string[] dummy) {
			OptimizedAttributes = null;
			ssSTGCS_ObjectMetadata = new STGCS_ObjectMetadataStructure(null);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[1];
			all[0] = null;
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
					ssSTGCS_ObjectMetadata.OptimizedAttributes = value[0];
				}
			}
			get {
				BitArray[] all = new BitArray[1];
				all[0] = null;
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssSTGCS_ObjectMetadata.Read(r, ref index);
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
		public void ReadIM(RCGCS_ObjectMetadataRecord r) {
			this = r;
		}


		public static bool operator == (RCGCS_ObjectMetadataRecord a, RCGCS_ObjectMetadataRecord b) {
			if (a.ssSTGCS_ObjectMetadata != b.ssSTGCS_ObjectMetadata) return false;
			return true;
		}

		public static bool operator != (RCGCS_ObjectMetadataRecord a, RCGCS_ObjectMetadataRecord b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(RCGCS_ObjectMetadataRecord)) return false;
			return (this == (RCGCS_ObjectMetadataRecord) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssSTGCS_ObjectMetadata.GetHashCode()
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

		public RCGCS_ObjectMetadataRecord(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssSTGCS_ObjectMetadata = new STGCS_ObjectMetadataStructure(null);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssSTGCS_ObjectMetadata", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSTGCS_ObjectMetadata' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSTGCS_ObjectMetadata = (STGCS_ObjectMetadataStructure) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
			ssSTGCS_ObjectMetadata.RecursiveReset();
		}

		public void InternalRecursiveSave() {
			ssSTGCS_ObjectMetadata.InternalRecursiveSave();
		}


		public RCGCS_ObjectMetadataRecord Duplicate() {
			RCGCS_ObjectMetadataRecord t;
			t.ssSTGCS_ObjectMetadata = (STGCS_ObjectMetadataStructure) this.ssSTGCS_ObjectMetadata.Duplicate();
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Record");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
			}
			if (detailLevel > 0) {
				ssSTGCS_ObjectMetadata.ToXml(this, recordElem, "GCS_ObjectMetadata", detailLevel - 1);
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "gcs_objectmetadata") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".GCS_ObjectMetadata")) variable.Value = ssSTGCS_ObjectMetadata; else variable.Optimized = true;
				variable.SetFieldName("gcs_objectmetadata");
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
			if (key == IdGCS_ObjectMetadata) {
				return ssSTGCS_ObjectMetadata;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssSTGCS_ObjectMetadata.FillFromOther((IRecord) other.AttributeGet(IdGCS_ObjectMetadata));
		}
		public bool IsDefault() {
			RCGCS_ObjectMetadataRecord defaultStruct = new RCGCS_ObjectMetadataRecord(null);
			if (this.ssSTGCS_ObjectMetadata != defaultStruct.ssSTGCS_ObjectMetadata) return false;
			return true;
		}
	} // RCGCS_ObjectMetadataRecord

	/// <summary>
	/// Structure <code>RCGCS_PrefixRecord</code>
	/// </summary>
	[Serializable()]
	public partial struct RCGCS_PrefixRecord: ISerializable, ITypedRecord<RCGCS_PrefixRecord> {
		internal static readonly GlobalObjectKey IdGCS_Prefix = GlobalObjectKey.Parse("2UmDmepsh0WSfJ_D1JexCA*Cjre3cgScEXb57igxa6wog");

		public static void EnsureInitialized() {}
		[System.Xml.Serialization.XmlElement("GCS_Prefix")]
		public STGCS_PrefixStructure ssSTGCS_Prefix;


		public static implicit operator STGCS_PrefixStructure(RCGCS_PrefixRecord r) {
			return r.ssSTGCS_Prefix;
		}

		public static implicit operator RCGCS_PrefixRecord(STGCS_PrefixStructure r) {
			RCGCS_PrefixRecord res = new RCGCS_PrefixRecord(null);
			res.ssSTGCS_Prefix = r;
			return res;
		}

		public BitArray OptimizedAttributes;

		public RCGCS_PrefixRecord(params string[] dummy) {
			OptimizedAttributes = null;
			ssSTGCS_Prefix = new STGCS_PrefixStructure(null);
		}

		public BitArray[] GetDefaultOptimizedValues() {
			BitArray[] all = new BitArray[1];
			all[0] = null;
			return all;
		}

		public BitArray[] AllOptimizedAttributes {
			set {
				if (value == null) {
				} else {
					ssSTGCS_Prefix.OptimizedAttributes = value[0];
				}
			}
			get {
				BitArray[] all = new BitArray[1];
				all[0] = null;
				return all;
			}
		}

		/// <summary>
		/// Read a record from database
		/// </summary>
		/// <param name="r"> Data base reader</param>
		/// <param name="index"> index</param>
		public void Read(IDataReader r, ref int index) {
			ssSTGCS_Prefix.Read(r, ref index);
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
		public void ReadIM(RCGCS_PrefixRecord r) {
			this = r;
		}


		public static bool operator == (RCGCS_PrefixRecord a, RCGCS_PrefixRecord b) {
			if (a.ssSTGCS_Prefix != b.ssSTGCS_Prefix) return false;
			return true;
		}

		public static bool operator != (RCGCS_PrefixRecord a, RCGCS_PrefixRecord b) {
			return !(a==b);
		}

		public override bool Equals(object o) {
			if (o.GetType() != typeof(RCGCS_PrefixRecord)) return false;
			return (this == (RCGCS_PrefixRecord) o);
		}

		public override int GetHashCode() {
			try {
				return base.GetHashCode()
				^ ssSTGCS_Prefix.GetHashCode()
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

		public RCGCS_PrefixRecord(SerializationInfo info, StreamingContext context) {
			OptimizedAttributes = null;
			ssSTGCS_Prefix = new STGCS_PrefixStructure(null);
			Type objInfo = this.GetType();
			FieldInfo fieldInfo = null;
			fieldInfo = objInfo.GetField("ssSTGCS_Prefix", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
			if (fieldInfo == null) {
				throw new Exception("The field named 'ssSTGCS_Prefix' was not found.");
			}
			if (fieldInfo.FieldType.IsSerializable) {
				ssSTGCS_Prefix = (STGCS_PrefixStructure) info.GetValue(fieldInfo.Name, fieldInfo.FieldType);
			}
		}

		public void RecursiveReset() {
			ssSTGCS_Prefix.RecursiveReset();
		}

		public void InternalRecursiveSave() {
			ssSTGCS_Prefix.InternalRecursiveSave();
		}


		public RCGCS_PrefixRecord Duplicate() {
			RCGCS_PrefixRecord t;
			t.ssSTGCS_Prefix = (STGCS_PrefixStructure) this.ssSTGCS_Prefix.Duplicate();
			t.OptimizedAttributes = null;
			return t;
		}

		IRecord IRecord.Duplicate() {
			return Duplicate();
		}

		public void ToXml(Object parent, System.Xml.XmlElement baseElem, String fieldName, int detailLevel) {
			System.Xml.XmlElement recordElem = VarValue.AppendChild(baseElem, "Record");
			if (fieldName != null) {
				VarValue.AppendAttribute(recordElem, "debug.field", fieldName);
			}
			if (detailLevel > 0) {
				ssSTGCS_Prefix.ToXml(this, recordElem, "GCS_Prefix", detailLevel - 1);
			} else {
				VarValue.AppendDeferredEvaluationElement(recordElem);
			}
		}

		public void EvaluateFields(VarValue variable, Object parent, String baseName, String fields) {
			String head = VarValue.GetHead(fields);
			String tail = VarValue.GetTail(fields);
			variable.Found = false;
			if (head == "gcs_prefix") {
				if (!VarValue.FieldIsOptimized(parent, baseName + ".GCS_Prefix")) variable.Value = ssSTGCS_Prefix; else variable.Optimized = true;
				variable.SetFieldName("gcs_prefix");
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
			if (key == IdGCS_Prefix) {
				return ssSTGCS_Prefix;
			} else {
				throw new Exception("Invalid key");
			}
		}
		public void FillFromOther(IRecord other) {
			if (other == null) return;
			ssSTGCS_Prefix.FillFromOther((IRecord) other.AttributeGet(IdGCS_Prefix));
		}
		public bool IsDefault() {
			RCGCS_PrefixRecord defaultStruct = new RCGCS_PrefixRecord(null);
			if (this.ssSTGCS_Prefix != defaultStruct.ssSTGCS_Prefix) return false;
			return true;
		}
	} // RCGCS_PrefixRecord
}
