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

using OpenSim.Framework;

using OpenMetaverse;
using OpenMetaverse.StructuredData;
using System.Threading.Tasks;

namespace WebRtcVoice
{
    // Presents interface for the voice capabilities to connect the user
    // to the voice server.
    public interface IWebRtcVoiceService
    {
        // The user is requesting a voice connection. The message contains the offer
        //     from the user and we must return the answer.
        // If there are problems, the returned map will contain an error message.

        // Initial calls to the voice server to get the user connected
        public Task<OSDMap> ProvisionVoiceAccountRequest(OSDMap pRequest, UUID pUserID, UUID pScene);
        public Task<OSDMap> VoiceSignalingRequest(OSDMap pRequest, UUID pUserID, UUID pScene);

        // Once connection state is looked up, the viewer session is passed in
        public Task<OSDMap> ProvisionVoiceAccountRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pScene);
        public Task<OSDMap> VoiceSignalingRequest(IVoiceViewerSession pVSession, OSDMap pRequest, UUID pUserID, UUID pScene);

        // Create a viewer session with all the variables needed for the underlying implementation
        public IVoiceViewerSession CreateViewerSession(OSDMap pRequest, UUID pUserID, UUID pScene);
    }
}
