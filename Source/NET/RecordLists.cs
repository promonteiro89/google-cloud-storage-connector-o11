using System;
using System.Data;
using System.Collections;
using System.Runtime.Serialization;
using System.Reflection;
using System.Xml;
using OutSystems.ObjectKeys;
using OutSystems.RuntimeCommon;
using OutSystems.HubEdition.RuntimePlatform;
using OutSystems.HubEdition.RuntimePlatform.Db;
using OutSystems.Internal.Db;
using OutSystems.HubEdition.RuntimePlatform.NewRuntime;

namespace OutSystems.NssGoogleCloudStorage_ext {

	/// <summary>
	/// RecordList type <code>RLGCS_ObjectRecordList</code> that represents a record list of
	///  <code>GCS_Object</code>
	/// </summary>
	[Serializable()]
	public partial class RLGCS_ObjectRecordList: GenericRecordList<RCGCS_ObjectRecord>, IEnumerable, IEnumerator, ISerializable {
		public static void EnsureInitialized() {}

		protected override RCGCS_ObjectRecord GetElementDefaultValue() {
			return new RCGCS_ObjectRecord("");
		}

		public T[] ToArray<T>(Func<RCGCS_ObjectRecord, T> converter) {
			return ToArray(this, converter);
		}

		public static T[] ToArray<T>(RLGCS_ObjectRecordList recordlist, Func<RCGCS_ObjectRecord, T> converter) {
			return InnerToArray(recordlist, converter);
		}
		public static implicit operator RLGCS_ObjectRecordList(RCGCS_ObjectRecord[] array) {
			RLGCS_ObjectRecordList result = new RLGCS_ObjectRecordList();
			result.InnerFromArray(array);
			return result;
		}

		public static RLGCS_ObjectRecordList ToList<T>(T[] array, Func <T, RCGCS_ObjectRecord> converter) {
			RLGCS_ObjectRecordList result = new RLGCS_ObjectRecordList();
			result.InnerFromArray(array, converter);
			return result;
		}

		public static RLGCS_ObjectRecordList FromRestList<T>(RestList<T> restList, Func <T, RCGCS_ObjectRecord> converter) {
			RLGCS_ObjectRecordList result = new RLGCS_ObjectRecordList();
			result.InnerFromRestList(restList, converter);
			return result;
		}
		/// <summary>
		/// Default Constructor
		/// </summary>
		public RLGCS_ObjectRecordList(): base() {
		}

		/// <summary>
		/// Constructor with transaction parameter
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_ObjectRecordList(IDbTransaction trans): base(trans) {
		}

		/// <summary>
		/// Constructor with transaction parameter and alternate read method
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		/// <param name="alternateReadDBMethod"> Alternate Read Method</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_ObjectRecordList(IDbTransaction trans, ReadDBMethodDelegate alternateReadDBMethod): this(trans) {
			this.alternateReadDBMethod = alternateReadDBMethod;
		}

		/// <summary>
		/// Constructor declaration for serialization
		/// </summary>
		/// <param name="info"> SerializationInfo</param>
		/// <param name="context"> StreamingContext</param>
		public RLGCS_ObjectRecordList(SerializationInfo info, StreamingContext context): base(info, context) {
		}

		public override BitArray[] GetDefaultOptimizedValues() {
			BitArray[] def = new BitArray[1];
			def[0] = null;
			return def;
		}
		/// <summary>
		/// Create as new list
		/// </summary>
		/// <returns>The new record list</returns>
		protected override OSList<RCGCS_ObjectRecord> NewList() {
			return new RLGCS_ObjectRecordList();
		}


	} // RLGCS_ObjectRecordList

	/// <summary>
	/// RecordList type <code>RLGCS_BucketRecordList</code> that represents a record list of
	///  <code>GCS_Bucket</code>
	/// </summary>
	[Serializable()]
	public partial class RLGCS_BucketRecordList: GenericRecordList<RCGCS_BucketRecord>, IEnumerable, IEnumerator, ISerializable {
		public static void EnsureInitialized() {}

		protected override RCGCS_BucketRecord GetElementDefaultValue() {
			return new RCGCS_BucketRecord("");
		}

		public T[] ToArray<T>(Func<RCGCS_BucketRecord, T> converter) {
			return ToArray(this, converter);
		}

		public static T[] ToArray<T>(RLGCS_BucketRecordList recordlist, Func<RCGCS_BucketRecord, T> converter) {
			return InnerToArray(recordlist, converter);
		}
		public static implicit operator RLGCS_BucketRecordList(RCGCS_BucketRecord[] array) {
			RLGCS_BucketRecordList result = new RLGCS_BucketRecordList();
			result.InnerFromArray(array);
			return result;
		}

		public static RLGCS_BucketRecordList ToList<T>(T[] array, Func <T, RCGCS_BucketRecord> converter) {
			RLGCS_BucketRecordList result = new RLGCS_BucketRecordList();
			result.InnerFromArray(array, converter);
			return result;
		}

		public static RLGCS_BucketRecordList FromRestList<T>(RestList<T> restList, Func <T, RCGCS_BucketRecord> converter) {
			RLGCS_BucketRecordList result = new RLGCS_BucketRecordList();
			result.InnerFromRestList(restList, converter);
			return result;
		}
		/// <summary>
		/// Default Constructor
		/// </summary>
		public RLGCS_BucketRecordList(): base() {
		}

		/// <summary>
		/// Constructor with transaction parameter
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_BucketRecordList(IDbTransaction trans): base(trans) {
		}

		/// <summary>
		/// Constructor with transaction parameter and alternate read method
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		/// <param name="alternateReadDBMethod"> Alternate Read Method</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_BucketRecordList(IDbTransaction trans, ReadDBMethodDelegate alternateReadDBMethod): this(trans) {
			this.alternateReadDBMethod = alternateReadDBMethod;
		}

		/// <summary>
		/// Constructor declaration for serialization
		/// </summary>
		/// <param name="info"> SerializationInfo</param>
		/// <param name="context"> StreamingContext</param>
		public RLGCS_BucketRecordList(SerializationInfo info, StreamingContext context): base(info, context) {
		}

		public override BitArray[] GetDefaultOptimizedValues() {
			BitArray[] def = new BitArray[1];
			def[0] = null;
			return def;
		}
		/// <summary>
		/// Create as new list
		/// </summary>
		/// <returns>The new record list</returns>
		protected override OSList<RCGCS_BucketRecord> NewList() {
			return new RLGCS_BucketRecordList();
		}


	} // RLGCS_BucketRecordList

	/// <summary>
	/// RecordList type <code>RLGCS_ObjectMetadataRecordList</code> that represents a record list of
	///  <code>GCS_ObjectMetadata</code>
	/// </summary>
	[Serializable()]
	public partial class RLGCS_ObjectMetadataRecordList: GenericRecordList<RCGCS_ObjectMetadataRecord>, IEnumerable, IEnumerator, ISerializable {
		public static void EnsureInitialized() {}

		protected override RCGCS_ObjectMetadataRecord GetElementDefaultValue() {
			return new RCGCS_ObjectMetadataRecord("");
		}

		public T[] ToArray<T>(Func<RCGCS_ObjectMetadataRecord, T> converter) {
			return ToArray(this, converter);
		}

		public static T[] ToArray<T>(RLGCS_ObjectMetadataRecordList recordlist, Func<RCGCS_ObjectMetadataRecord, T> converter) {
			return InnerToArray(recordlist, converter);
		}
		public static implicit operator RLGCS_ObjectMetadataRecordList(RCGCS_ObjectMetadataRecord[] array) {
			RLGCS_ObjectMetadataRecordList result = new RLGCS_ObjectMetadataRecordList();
			result.InnerFromArray(array);
			return result;
		}

		public static RLGCS_ObjectMetadataRecordList ToList<T>(T[] array, Func <T, RCGCS_ObjectMetadataRecord> converter) {
			RLGCS_ObjectMetadataRecordList result = new RLGCS_ObjectMetadataRecordList();
			result.InnerFromArray(array, converter);
			return result;
		}

		public static RLGCS_ObjectMetadataRecordList FromRestList<T>(RestList<T> restList, Func <T, RCGCS_ObjectMetadataRecord> converter) {
			RLGCS_ObjectMetadataRecordList result = new RLGCS_ObjectMetadataRecordList();
			result.InnerFromRestList(restList, converter);
			return result;
		}
		/// <summary>
		/// Default Constructor
		/// </summary>
		public RLGCS_ObjectMetadataRecordList(): base() {
		}

		/// <summary>
		/// Constructor with transaction parameter
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_ObjectMetadataRecordList(IDbTransaction trans): base(trans) {
		}

		/// <summary>
		/// Constructor with transaction parameter and alternate read method
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		/// <param name="alternateReadDBMethod"> Alternate Read Method</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_ObjectMetadataRecordList(IDbTransaction trans, ReadDBMethodDelegate alternateReadDBMethod): this(trans) {
			this.alternateReadDBMethod = alternateReadDBMethod;
		}

		/// <summary>
		/// Constructor declaration for serialization
		/// </summary>
		/// <param name="info"> SerializationInfo</param>
		/// <param name="context"> StreamingContext</param>
		public RLGCS_ObjectMetadataRecordList(SerializationInfo info, StreamingContext context): base(info, context) {
		}

		public override BitArray[] GetDefaultOptimizedValues() {
			BitArray[] def = new BitArray[1];
			def[0] = null;
			return def;
		}
		/// <summary>
		/// Create as new list
		/// </summary>
		/// <returns>The new record list</returns>
		protected override OSList<RCGCS_ObjectMetadataRecord> NewList() {
			return new RLGCS_ObjectMetadataRecordList();
		}


	} // RLGCS_ObjectMetadataRecordList

	/// <summary>
	/// RecordList type <code>RLGCS_PrefixRecordList</code> that represents a record list of
	///  <code>GCS_Prefix</code>
	/// </summary>
	[Serializable()]
	public partial class RLGCS_PrefixRecordList: GenericRecordList<RCGCS_PrefixRecord>, IEnumerable, IEnumerator, ISerializable {
		public static void EnsureInitialized() {}

		protected override RCGCS_PrefixRecord GetElementDefaultValue() {
			return new RCGCS_PrefixRecord("");
		}

		public T[] ToArray<T>(Func<RCGCS_PrefixRecord, T> converter) {
			return ToArray(this, converter);
		}

		public static T[] ToArray<T>(RLGCS_PrefixRecordList recordlist, Func<RCGCS_PrefixRecord, T> converter) {
			return InnerToArray(recordlist, converter);
		}
		public static implicit operator RLGCS_PrefixRecordList(RCGCS_PrefixRecord[] array) {
			RLGCS_PrefixRecordList result = new RLGCS_PrefixRecordList();
			result.InnerFromArray(array);
			return result;
		}

		public static RLGCS_PrefixRecordList ToList<T>(T[] array, Func <T, RCGCS_PrefixRecord> converter) {
			RLGCS_PrefixRecordList result = new RLGCS_PrefixRecordList();
			result.InnerFromArray(array, converter);
			return result;
		}

		public static RLGCS_PrefixRecordList FromRestList<T>(RestList<T> restList, Func <T, RCGCS_PrefixRecord> converter) {
			RLGCS_PrefixRecordList result = new RLGCS_PrefixRecordList();
			result.InnerFromRestList(restList, converter);
			return result;
		}
		/// <summary>
		/// Default Constructor
		/// </summary>
		public RLGCS_PrefixRecordList(): base() {
		}

		/// <summary>
		/// Constructor with transaction parameter
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_PrefixRecordList(IDbTransaction trans): base(trans) {
		}

		/// <summary>
		/// Constructor with transaction parameter and alternate read method
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		/// <param name="alternateReadDBMethod"> Alternate Read Method</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_PrefixRecordList(IDbTransaction trans, ReadDBMethodDelegate alternateReadDBMethod): this(trans) {
			this.alternateReadDBMethod = alternateReadDBMethod;
		}

		/// <summary>
		/// Constructor declaration for serialization
		/// </summary>
		/// <param name="info"> SerializationInfo</param>
		/// <param name="context"> StreamingContext</param>
		public RLGCS_PrefixRecordList(SerializationInfo info, StreamingContext context): base(info, context) {
		}

		public override BitArray[] GetDefaultOptimizedValues() {
			BitArray[] def = new BitArray[1];
			def[0] = null;
			return def;
		}
		/// <summary>
		/// Create as new list
		/// </summary>
		/// <returns>The new record list</returns>
		protected override OSList<RCGCS_PrefixRecord> NewList() {
			return new RLGCS_PrefixRecordList();
		}


	} // RLGCS_PrefixRecordList

	/// <summary>
	/// RecordList type <code>RLGCS_MetadataEntryRecordList</code> that represents a record list of
	///  <code>GCS_MetadataEntry</code>
	/// </summary>
	[Serializable()]
	public partial class RLGCS_MetadataEntryRecordList: GenericRecordList<RCGCS_MetadataEntryRecord>, IEnumerable, IEnumerator, ISerializable {
		public static void EnsureInitialized() {}

		protected override RCGCS_MetadataEntryRecord GetElementDefaultValue() {
			return new RCGCS_MetadataEntryRecord("");
		}

		public T[] ToArray<T>(Func<RCGCS_MetadataEntryRecord, T> converter) {
			return ToArray(this, converter);
		}

		public static T[] ToArray<T>(RLGCS_MetadataEntryRecordList recordlist, Func<RCGCS_MetadataEntryRecord, T> converter) {
			return InnerToArray(recordlist, converter);
		}
		public static implicit operator RLGCS_MetadataEntryRecordList(RCGCS_MetadataEntryRecord[] array) {
			RLGCS_MetadataEntryRecordList result = new RLGCS_MetadataEntryRecordList();
			result.InnerFromArray(array);
			return result;
		}

		public static RLGCS_MetadataEntryRecordList ToList<T>(T[] array, Func <T, RCGCS_MetadataEntryRecord> converter) {
			RLGCS_MetadataEntryRecordList result = new RLGCS_MetadataEntryRecordList();
			result.InnerFromArray(array, converter);
			return result;
		}

		public static RLGCS_MetadataEntryRecordList FromRestList<T>(RestList<T> restList, Func <T, RCGCS_MetadataEntryRecord> converter) {
			RLGCS_MetadataEntryRecordList result = new RLGCS_MetadataEntryRecordList();
			result.InnerFromRestList(restList, converter);
			return result;
		}
		/// <summary>
		/// Default Constructor
		/// </summary>
		public RLGCS_MetadataEntryRecordList(): base() {
		}

		/// <summary>
		/// Constructor with transaction parameter
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_MetadataEntryRecordList(IDbTransaction trans): base(trans) {
		}

		/// <summary>
		/// Constructor with transaction parameter and alternate read method
		/// </summary>
		/// <param name="trans"> IDbTransaction Parameter</param>
		/// <param name="alternateReadDBMethod"> Alternate Read Method</param>
		[Obsolete("Use the Default Constructor and set the Transaction afterwards.")]
		public RLGCS_MetadataEntryRecordList(IDbTransaction trans, ReadDBMethodDelegate alternateReadDBMethod): this(trans) {
			this.alternateReadDBMethod = alternateReadDBMethod;
		}

		/// <summary>
		/// Constructor declaration for serialization
		/// </summary>
		/// <param name="info"> SerializationInfo</param>
		/// <param name="context"> StreamingContext</param>
		public RLGCS_MetadataEntryRecordList(SerializationInfo info, StreamingContext context): base(info, context) {
		}

		public override BitArray[] GetDefaultOptimizedValues() {
			BitArray[] def = new BitArray[1];
			def[0] = null;
			return def;
		}
		/// <summary>
		/// Create as new list
		/// </summary>
		/// <returns>The new record list</returns>
		protected override OSList<RCGCS_MetadataEntryRecord> NewList() {
			return new RLGCS_MetadataEntryRecordList();
		}


	} // RLGCS_MetadataEntryRecordList
}
