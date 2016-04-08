RailInfra region module
=======================

This region module for opensim allows running a
rail network (or basically, anything that involves
moving objects following pre-set tracks) without
depending on lsl/osl script functions.

This is for now, an experiment and a work in progress.

Configuration
-------------

Add the following lines to bin/OpenSim.ini:

[RailInfraModule]
    ; UUID of the manager agent, not implemented yet
    ManagerUUID = "c87a7277-b137-44c4-9971-82f5bb463dc0"

    ; channel to listen on for rail network objects to interface
    Channel = 23

    ; distance in m to scan forward for the next Guide
    TrackPointDistance = 12

    ; angle of cone to scan forward for the next Guide
    TrackPointAngle = 0.16

Operation
---------

On start-up of a region, the module scans for prims named
"Guide" or "Alt Guide". These prims are supposed to be layed
out so as to form a railway network in the SLRR standard [1].

The guides may be phantom, however if compatibility with
SLRR and VRC [2] vehicles is required, the guides must not
be phantom.

Vehicles designed for the RailInfra module will run fine
with phantom guides.

The module will build a model for the railway networks present
in the regions registered. This allows the module to move trains
along the tracks.

Vehicles register with the module, after which control of
position and rotation of the vehicle is by the region module.

Reference in-world Guides and Switch installations will be
provided, for now refer to the examle .oar files in bin/.

Status
------

Currently working on the track scanning, mostly needs code
for merging switches.

To implement are:
- vehicle operation
- status reporting (for in-world status displays)
- refinement of console interface
- articulated vehicles

References
----------

[1] SLRR Standard, http://
