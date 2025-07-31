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

using System.Threading.Tasks;

using OMV = OpenMetaverse;

namespace WebRtcVoice
{
    public interface IVoiceViewerSession
    {
        // This ID is passed to and from the viewer to identify the session
        public string ViewerSessionID { get; set; }
        public IWebRtcVoiceService VoiceService { get; set; }
        // THis ID is passed between us and the voice service to idetify the session
        public string VoiceServiceSessionId { get; set; }
        // The UUID of the region that is being connected to
        public OMV.UUID RegionId { get; set; }

        // The simulator has a GUID to identify the user
        public OMV.UUID AgentId { get; set; }

        // Disconnect the connection to the voice service for this session
        public Task Shutdown();
    }
}
