﻿// Copyright 2015 - 2017 Andreas Jakl. All rights reserved. 
// https://github.com/andijakl/universal-beacon 
// 
// Based on the Eddystone specification by Google, 
// available under Apache License, Version 2.0 from
// https://github.com/google/eddystone
// 
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
// 
//    http://www.apache.org/licenses/LICENSE-2.0 
// 
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License. 

using System;
using System.Collections.ObjectModel;
using UniversalBeacon.Library.Core.Interfaces;
using UniversalBeacon.Library.Core.Interop;

namespace UniversalBeacon.Library.Core.Entities
{
    /// <summary>
    /// Manages multiple beacons and distributes received Bluetooth LE
    /// Advertisements based on unique Bluetooth beacons.
    /// 
    /// Whenever your app gets a callback that it has received a new Bluetooth LE
    /// advertisement, send it to the ReceivedAdvertisement() method of this class,
    /// which will handle the data and either add a new Bluetooth beacon to the list
    /// of beacons observed so far, or update a previously known beacon with the
    /// new advertisement information.
    /// </summary>
    public class BeaconManager
    {
        public event EventHandler<BeaconChangedEventArgs> BeaconAdded;
        public event EventHandler<BeaconChangedEventArgs> BeaconUpdated;

        private IBluetoothPacketProvider m_provider;
        private Action<Action> m_invokeAction;

        /// <summary>
        /// Constructs a Beacon Manager
        /// </summary>
        /// <param name="provider">A platform-specific BLE advertisement packet provider</param>
        /// <param name="invokeAction">An optional Action to synchronize marshaling the population of the Beacons collection to a UI thread</param>
        /// <example><code>
        ///  _beaconManager = new BeaconManager(provider, async (action) =>
        ///  {
        ///    await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { action(); });
        ///  });
        /// </code></example>
        public BeaconManager(IBluetoothPacketProvider provider, Action<Action> invokeAction = null)
        {
            if (provider == null)
            {
                throw new ArgumentNullException("provider");
            }

            m_provider = provider;
            m_provider.AdvertisementPacketReceived += OnAdvertisementPacketReceived;
            m_invokeAction = invokeAction;
        }

        /// <summary>
        /// List of known beacons so far, which all have a unique Bluetooth MAC address
        /// and can have multiple data frames.
        /// </summary>
        public ObservableCollection<Beacon> BluetoothBeacons { get; set; } = new ObservableCollection<Beacon>();

        /// <summary>
        /// Start listening for beacon packets
        /// </summary>
        public void Start()
        {
            m_provider.Start();
        }

        /// <summary>
        /// Stop listening for beacon packets
        /// </summary>
        public void Stop()
        {
            m_provider.Stop();
        }

        /// <summary>
        /// Event handler called when the constructor-provided IBluetoothPacketManager receives an advertisement packet
        /// </summary>
        /// <param name="sender">The instance of the IBluetoothPacketManager sending the event</param>
        /// <param name="e">The arguments containing the advertisment packet data</param>
        private void OnAdvertisementPacketReceived(object sender, BLEAdvertisementPacketArgs e)
        {
            if (m_invokeAction != null)
            {
                m_invokeAction(() => { ReceivedAdvertisement(e.Data); });
            }
            else
            {
                ReceivedAdvertisement(e.Data);
            }
        }

        /// <summary>
        /// Analyze the received Bluetooth LE advertisement, and either add a new unique
        /// beacon to the list of known beacons, or update a previously known beacon
        /// with the new information.
        /// </summary>
        /// <param name="btAdv">Bluetooth advertisement to parse, as received from
        /// the Windows Bluetooth LE API.</param>
        private void ReceivedAdvertisement(BLEAdvertisementPacket btAdv)
        {
            if (btAdv == null) return;

            // Check if we already know this bluetooth address
            foreach (var bluetoothBeacon in BluetoothBeacons)
            {
                if (bluetoothBeacon.BluetoothAddress == btAdv.BluetoothAddress)
                {
                    // We already know this beacon
                    // Update / Add info to existing beacon
                    bluetoothBeacon.UpdateBeacon(btAdv);
                    BeaconUpdated?.Invoke(this, new BeaconChangedEventArgs(bluetoothBeacon));
                    return;
                }
            }

            // Beacon was not yet known - add it to the list.
            var newBeacon = new Beacon(btAdv);
            BluetoothBeacons.Add(newBeacon);
            BeaconAdded?.Invoke(this, new BeaconChangedEventArgs(newBeacon));
        }
    }
}
