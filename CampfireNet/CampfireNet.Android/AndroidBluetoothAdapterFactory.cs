using Android.App;
using Android.Bluetooth;
using Android.Content;
using AndroidTest.Droid;

namespace CampfireNet {
   public class AndroidBluetoothAdapterFactory {
      public AndroidBluetoothAdapter Create(Activity currentActivity, Context applicationContext, BluetoothAdapter nativeBluetoothAdapter) {
         var automaticPairingService = new AutomaticPairingService(applicationContext);
         var bluetoothFacade = new AndroidBluetoothFacade(applicationContext);
         bluetoothFacade.EnableBluetoothFromActivity(currentActivity);

         var bluetoothDiscoveryFacade = new BluetoothDiscoveryFacade(applicationContext);
         var inboundBluetoothSocketTable = new InboundBluetoothSocketTable();
         var bluetoothServer = BluetoothServer.Create(nativeBluetoothAdapter, inboundBluetoothSocketTable);
         bluetoothServer.Start();
         var campfireNetBluetoothAdapter = new AndroidBluetoothAdapter(applicationContext, nativeBluetoothAdapter, bluetoothDiscoveryFacade, inboundBluetoothSocketTable);
         return campfireNetBluetoothAdapter;
      }
   }
}