package OpenSim::UserServer::Config;

use strict;

our %SYS_SQL = (
	select_user_by_name =>
	"select * from users where username=? and lastname=?",
	select_user_by_uuid =>
	"select * from users where uuid=?",
	create_user =>
	"insert into users values(?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)",
);

our @USERS_COLUMNS = (
	"UUID",
	"username",
	"lastname",
	"passwordHash",
	"passwordSalt",
	"homeRegion",
	"homeLocationX",
	"homeLocationY",
	"homeLocationZ",
	"homeLookAtX",
	"homeLookAtY",
	"homeLookAtZ",
	"created",
	"lastLogin",
	"userInventoryURI",
	"userAssetURI",
	"profileCanDoMask",
	"profileWantDoMask",
	"profileAboutText",
	"profileFirstText",
	"profileImage",
	"profileFirstImage",
);

# copied from opensim
our @classified_categories = (
	{ category_id => 1, category_name => "Shopping" },
	{ category_id => 2, category_name => "Land Rental" },
	{ category_id => 3, category_name => "Property Rental" },
	{ category_id => 4, category_name => "Special Attraction" },
	{ category_id => 5, category_name => "New Products" },
	{ category_id => 6, category_name => "Employment" },
	{ category_id => 7, category_name => "Wanted" },
	{ category_id => 8, category_name => "Service" },
	{ category_id => 9, category_name => "Personal" },
);

our @event_categories = ();
our @event_notifications = ();
our @gestures =();
our @global_textures = (
	{
		cloud_texture_id => "dc4b9f0b-d008-45c6-96a4-01dd947ac621",
		moon_texture_id  => "ec4b9f0b-d008-45c6-96a4-01dd947ac621",
		sun_texture_id   => "cce0f112-878f-4586-a2e2-a8f104bba271",
	},
);
our @initial_outfit = (
	{ folder_name => "Nightclub Female", gender => "female" }
);
our @inventory_lib_owner = ({ agent_id => "11111111-1111-0000-0000-000100bba000" });
our @inventory_lib_root = ({ folder_id => "00000112-000f-0000-0000-000100bba000" });
our @inventory_root = ({ folder_id => "2eb27bc2-22ee-48db-b2e9-5c79a6582919" });
our @inventory_skel_lib = (
	{
		folder_id    => "00000112-000f-0000-0000-000100bba000",
		name         => "OpenSim Library",
		parent_id    => "00000000-0000-0000-0000-000000000000",
		type_default => -1,
		version      => 1,
	},
	{
		folder_id    => "00000112-000f-0000-0000-000100bba001",
		name         => "Texture Library",
		parent_id    => "00000112-000f-0000-0000-000100bba000",
		type_default => -1,
		version      => 1,
	},
);
our @inventory_skeleton = (
	{
		folder_id    => "2eb27bc2-22ee-48db-b2e9-5c79a6582919",
		name         => "My Inventory",
		parent_id    => "00000000-0000-0000-0000-000000000000",
		type_default => 8,
		version      => 1,
	},
	{
		folder_id    => "6cc20d86-9945-4997-a102-959348d56821",
		name         => "Textures",
		parent_id    => "2eb27bc2-22ee-48db-b2e9-5c79a6582919",
		type_default => 0,
		version      => 1,
	},
	{
		folder_id    => "840b747f-bb7d-465e-ab5a-58badc953484",
		name         => "Clothes",
	 	parent_id    => "2eb27bc2-22ee-48db-b2e9-5c79a6582919",
		type_default => 5,
		version      => 1,
	},
	{
		folder_id    => "37039005-7bbe-42a2-aa12-6bda453f37fd",
		name         => "Objects",
		parent_id    => "2eb27bc2-22ee-48db-b2e9-5c79a6582919",
		type_default => 6,
		version      => 1,
	},
);
our @login_flags = (
	{
		daylight_savings    => "N",
		ever_logged_in      => "Y",
		gendered            => "Y",
		stipend_since_login => "N",
	},
);
our @ui_config = ({ allow_first_life => "Y" });

1;

