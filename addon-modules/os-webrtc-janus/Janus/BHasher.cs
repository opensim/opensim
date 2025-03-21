// Copyright 2024 Robert Adams (misterblue@misterblue.com)
//
// This Source Code Form is subject to the terms of the Mozilla Public
// License, v. 2.0. If a copy of the MPL was not distributed with this
// file, You can obtain one at http://mozilla.org/MPL/2.0/.
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Security.Cryptography;

namespace WebRtcVoice
{

    // There are several different hashing systems ranging from int's to SHA versions.
    // The model here is to create a hasher of the desired type, do Add's of things to
    //    hash, and complete with a Finish() to return a BHash that contains the hash value.
    // Since some hash functions are incremental (doing Add's) while some are buffer
    //    oriented (create a hash of a byte buffer), the interface is built to cover both.
    //    Some optimizations are implemented internally (like not copying the buffer
    //    for buffer based hashers if Finish(bytes) is used).
    //
    // var hasher = new BHashSHA256();
    // BHash bHash = hasher.Finish(buffer);
    // byte[] theHash = bHash.ToByte();
    //
    // Note that BHash has IEquatable and IComparible so it can be used in Dictionaries
    //     and sorted Lists.
    //
    // The C# GetHashCode() method returns an int that is usually based on location.
    //     Signatures should really be at least 64 bits so these routines generate
    //     ulong's for hashes and fold them to make the int for GetHashCode().

    // Create a BHasher, do a bunch of 'Add's, then Finish().
    public interface IBHasher {
        // Create a new object implementing BHasher, then Add values to be hashed
        void Add(byte c);
        void Add(ushort c);
        void Add(uint c);
        void Add(ulong c);
        void Add(float c);
        void Add(byte[] c, int offset, int len);
        void Add(string c);
        void Add(BHash c);

        BHash Finish();
        // Finish and add byte array. 
        // If no Add's before, can do the hashing without copying the byte array
        BHash Finish(byte[] c);
        BHash Finish(byte[] c, int offset, int len);
        // Get the hash code after doing a Finish()
        BHash Hash();
    }

    // ======================================================================
    // BHasher computes a BHash which holds the hash value after Finish() is called.
    public abstract class BHash : IEquatable<BHash>, IComparable<BHash> {
        public abstract override string ToString();
        public abstract byte[] ToBytes();
        public abstract ulong ToULong();  // returns the hash of the hash if not int based hash
        public abstract bool Equals(BHash other);
        public abstract int CompareTo(BHash obj);
        // public abstract int Compare(BHash x, BHash y);   // TODO: do we need this function?
        public abstract override int GetHashCode(); // to match the C# standard hash function
    }

    // A hash that is an UInt64
    public class BHashULong : BHash {
        protected ulong _hash;

        public BHashULong() {
            _hash = 5131;
        }
        public BHashULong(ulong initialHash) {
            _hash = initialHash;
        }
        // the .NET GetHashCode uses an int. Make conversion easy.
        public BHashULong(int initialHash) {
            _hash = (ulong)initialHash;
        }
        public override string ToString() {
            return _hash.ToString();
        }
        public override byte[] ToBytes() {
            return BitConverter.GetBytes(_hash);
        }
        public override ulong ToULong() {
            return _hash;
        }
        public override bool Equals(BHash other) {
            bool ret = false;
            if (other != null) {
                BHash bh = other as BHashULong;
                if (bh != null) {
                    ret = _hash.Equals(bh.ToULong());
                }
            }
            return ret;
        }
        public override int CompareTo(BHash other) {
            int ret = 1;
            if (other != null) {
                if (other is BHashULong bh) {
                    ret = _hash.CompareTo(bh.ToULong());
                }
            }
            return ret;
        }
        public override int GetHashCode() {
            ulong upper = (_hash >> 32) & 0xffffffff;
            ulong lower = _hash & 0xffffffff;
            return (int)(upper ^ lower);
        }
    }

    // A hash that is an array of bytes
    public class BHashBytes : BHash {
        private readonly byte[] _hash;

        public BHashBytes() {
            _hash = new byte[0];
        }
        public BHashBytes(byte[] initialHash) {
            _hash = initialHash;
        }
        public override string ToString() {
            // BitConverter puts a hyphen between each byte. Remove them
            return BitConverter.ToString(_hash).Replace("-", String.Empty);
        }
        public override byte[] ToBytes() {
            return _hash;
        }
        public override ulong ToULong() {
            return this.MakeHashCode();
        }
        public override bool Equals(BHash other) {
            bool ret = false;
            if (other != null) {
                BHash bh = other as BHashBytes;
                if (bh != null) {
                    ret = _hash.Equals(bh.ToBytes());
                }
            }
            return ret;
        }
        public override int CompareTo(BHash other) {
            int ret = 1;
            if (other != null) {
                BHash bh = other as BHashBytes;
                if (bh != null) {
                    byte[] otherb = bh.ToBytes();
                    if (_hash.Length != otherb.Length) {
                        ret = _hash.Length.CompareTo(otherb.Length);
                    }
                    else {
                        ret = 0;    // start off assuming they are equal
                        for (int ii = 0; ii < _hash.Length; ii++) {
                            ret = _hash[ii].CompareTo(otherb[ii]);
                            if (ret != 0) break;
                        }
                    }
                }
            }
            return ret;
        }
        public override int GetHashCode()
        {
            ulong hashhash = this.MakeHashCode();
            ulong upper = (hashhash >> 32 )& 0xffffffff;
            ulong lower = hashhash & 0xffffffff;
            return (int)(upper ^ lower);
        }
        public ulong MakeHashCode() {
            ulong h = 5381;
            for (int ii = 0; ii < _hash.Length; ii++) {
                h = ((h << 5) + h) + (ulong)(_hash[ii]);
            }
            return h;
        }

    }

    // ======================================================================
    // ======================================================================
    public abstract class BHasher : IBHasher
    {
        public BHasher() {
        }

        public abstract void Add(byte c);
        public abstract void Add(ushort c);
        public abstract void Add(uint c);
        public abstract void Add(ulong c);
        public abstract void Add(float c);
        public abstract void Add(byte[] c, int offset, int len);
        public abstract void Add(string c);
        public abstract void Add(BHash c);
        public abstract BHash Finish();
        public abstract BHash Finish(byte[] c);
        public abstract BHash Finish(byte[] c, int offset, int len);
        public abstract BHash Hash();
    }

    // A hasher that builds up a buffer of bytes ('building') and then hashes over same
    public abstract class BHasherBytes : BHasher {
        protected byte[] building;
        protected int buildingLoc; 
        protected int allocStep = 1024;

        public BHasherBytes() : base() {
            building = new byte[allocStep];
            buildingLoc = 0;
        }

        private byte[] oneByte = new byte[1];
        public override void Add(byte c) {
            oneByte[0] = c;
            AddBytes(oneByte, 0, 1);
        }

        public override void Add(ushort c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, 0, bytes.Length);
        }

        public override void Add(uint c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, 0, bytes.Length);
        }

        public override void Add(ulong c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, 0, bytes.Length);
        }

        public override void Add(float c) {
            byte[] bytes = BitConverter.GetBytes(c);
            AddBytes(bytes, 0, bytes.Length);
        }

        public override void Add(byte[] c, int offset, int len) {
            AddBytes(c, offset, len);
        }

        public override void Add(string c)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(c);
            AddBytes(bytes, 0, bytes.Length);
        }

        public override void Add(BHash c) {
            byte[] bytes = c.ToBytes();
            AddBytes(bytes, 0, bytes.Length);
        }

        // Implemented by derived class
        // public abstract BHash Finish();

        // Helper function for simple byte array
        public override BHash Finish(byte[] c) {
            return this.Finish(c, 0, c.Length);
        }

        // Implemented by derived class
        // public abstract BHash Finish(byte[] c, int offset, int len);

        // Implemented by derived class
        //  public abstract BHash Hash();

        // Add the given number of bytes to the byte array being built
        protected void AddBytes(byte[] addition, int offset, int len) {
            // byte[] tempBytes = new byte[len];                   // DEBUG DEBUG
            // Array.Copy(addition, offset, tempBytes, 0, len);    // DEBUG DEBUG
            // System.Console.WriteLine(String.Format("AddBytes: offset={0}, len={1}, bytes={2}",  // DEBUG DEBUG
            //    offset, len, BitConverter.ToString(tempBytes).Replace("-", String.Empty)));      // DEBUG DEBUG
            if (len < 0 || offset < 0 || addition == null) {
                throw new ArgumentException(String.Format("BHasherBytes.AddBytes: Bad parameters. offset={0}, len={1}",
                                offset, len));
            }
            if (offset + len > addition.Length) {
                throw new ArgumentException(String.Format("BHasherBytes.AddBytes: addition parameters off end of array. addition.len={0}, offset={1}, len={2}",
                                addition.Length, offset, len));
            }
            if (len > 0) {
                if (buildingLoc + len > building.Length) {
                    // New data requires expanding the data buffer
                    byte[] newBuilding = new byte[buildingLoc + len + allocStep];
                    Buffer.BlockCopy(building, 0, newBuilding, 0, buildingLoc);
                    building = newBuilding;
                }
                Buffer.BlockCopy(addition, offset, building, buildingLoc, len);
                buildingLoc += len;
            }
        }
    }

    // ======================================================================
    // ULong hash code taken from Meshmerizer
    public class BHasherMdjb2 : BHasherBytes, IBHasher {
        BHashULong hash = new BHashULong();

        public BHasherMdjb2() : base() {
        }

        public override BHash Finish() {
            hash = new BHashULong(ComputeMdjb2Hash(building, 0, buildingLoc));
            return hash;
        }

        public override BHash Finish(byte[] c, int offset, int len) {
            if (building.Length > 0) {
                AddBytes(c, offset, len);
                hash = new BHashULong(ComputeMdjb2Hash(building, 0, buildingLoc));
            }
            else {
                // if no 'Add's were done, don't copy the input data
                hash = new BHashULong(ComputeMdjb2Hash(c, offset, len));
            }
            return hash;
        }

        private ulong ComputeMdjb2Hash(byte[] c, int offset, int len) {
            ulong h = 5381;
            for (int ii = offset; ii < offset+len; ii++) {
                h = ((h << 5) + h) + (ulong)(c[ii]);
            }
            return h;
        }

        public override BHash Hash() {
            return hash;
        }
    }

    // ======================================================================
    public class BHasherMD5 : BHasherBytes, IBHasher {
        BHashBytes hash = new BHashBytes();

        public BHasherMD5() : base() {
        }

        public override BHash Finish() {
            MD5 md5 = MD5.Create();
            hash = new BHashBytes(md5.ComputeHash(building, 0, buildingLoc));
            return hash;
        }

        public override BHash Finish(byte[] c) {
            return this.Finish(c, 0, c.Length);
        }

        public override BHash Finish(byte[] c, int offset, int len) {
            MD5 md5 = MD5.Create();
            if (building.Length > 0) {
                AddBytes(c, offset, len);
                hash = new BHashBytes(md5.ComputeHash(building, 0, buildingLoc));
            }
            else {
                // if no 'Add's were done, don't copy the input data
                hash = new BHashBytes(md5.ComputeHash(c, offset, len));
            }
            return hash;
        }

        public override BHash Hash() {
            return hash;
        }
    }

    // ======================================================================
    public class BHasherSHA256 : BHasherBytes, IBHasher {
        BHashBytes hash = new BHashBytes();

        public BHasherSHA256() : base() {
        }

        public override BHash Finish() {
            using (SHA256 SHA256 = SHA256.Create()) {
                hash = new BHashBytes(SHA256.ComputeHash(building, 0, buildingLoc));
            }
            return hash;
        }

        public override BHash Finish(byte[] c) {
            return this.Finish(c, 0, c.Length);
        }

        public override BHash Finish(byte[] c, int offset, int len) {
            using (SHA256 SHA256 = SHA256.Create()) {
                if (buildingLoc > 0) {
                    AddBytes(c, offset, len);
                    hash = new BHashBytes(SHA256.ComputeHash(building, 0, buildingLoc));
                }
                else {
                    // if no 'Add's were done, don't copy the input data
                    hash = new BHashBytes(SHA256.ComputeHash(c, offset, len));
                }
            }
            return hash;
        }

        public override BHash Hash() {
            return hash;
        }
    }
}
