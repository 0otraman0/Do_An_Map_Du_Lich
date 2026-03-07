package crc64ad9e67ddf78c28be;


public class PinWrapper
	extends java.lang.Object
	implements
		mono.android.IGCUserPeer
{
/** @hide */
	public static final String __md_methods;
	static {
		__md_methods = 
			"";
		mono.android.Runtime.register ("MauiAppMain.PinWrapper, MauiAppMain", PinWrapper.class, __md_methods);
	}

	public PinWrapper ()
	{
		super ();
		if (getClass () == PinWrapper.class) {
			mono.android.TypeManager.Activate ("MauiAppMain.PinWrapper, MauiAppMain", "", this, new java.lang.Object[] {  });
		}
	}

	private java.util.ArrayList refList;
	public void monodroidAddReference (java.lang.Object obj)
	{
		if (refList == null)
			refList = new java.util.ArrayList ();
		refList.add (obj);
	}

	public void monodroidClearReferences ()
	{
		if (refList != null)
			refList.clear ();
	}
}
